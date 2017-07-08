﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using CodeImp.DoomBuilder.Windows;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Plugins;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public partial class ThreeDFloorEditorWindow : Form
	{
		List<ThreeDFloor> threedfloors;
		List<Sector> selectedsectors;
		List<ThreeDFloorHelperControl> controlpool;

		public List<Sector> SelectedSectors { get { return selectedsectors; } }

		public List<ThreeDFloor> ThreeDFloors { get { return threedfloors; } set { threedfloors = value; } }

		public ThreeDFloorEditorWindow()
		{
			this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width/2, Screen.PrimaryScreen.WorkingArea.Height/2);
			controlpool = new List<ThreeDFloorHelperControl>();
			InitializeComponent();
		}

		private void ThreeDFloorEditorWindow_Load(object sender, EventArgs e)
		{
			selectedsectors = new List<Sector>(General.Map.Map.GetSelectedSectors(true));
			sharedThreeDFloorsCheckBox.Checked = false;
			FillThreeDFloorPanel(threedfloors);
		}

		// Gets a control from the pool or creates a new one
		private ThreeDFloorHelperControl GetThreeDFloorControl()
		{
			ThreeDFloorHelperControl ctrl = controlpool.FirstOrDefault(o => o.Used == false);

			if (ctrl == null)
			{
				ctrl = new ThreeDFloorHelperControl();
				ctrl.Used = true;
				controlpool.Add(ctrl);
				threeDFloorPanel.Controls.Add(ctrl);
			}
			else
			{
				ctrl.SetDefaults();
				ctrl.Used = true;
			}

			return ctrl;
		}

		private void okButton_Click(object sender, EventArgs e)
		{
			threedfloors = new List<ThreeDFloor>();

			foreach (ThreeDFloorHelperControl ctrl in threeDFloorPanel.Controls.OfType<ThreeDFloorHelperControl>())
			{
				if (ctrl.Used)
				{
					ctrl.ApplyToThreeDFloor();
					threedfloors.Add(ctrl.ThreeDFloor);
				}
			}

			this.DialogResult = DialogResult.OK;
			this.Close();
		}

		private void cancelButton_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;
			this.Close();
		}

		public void addThreeDFloorButton_Click(object sender, EventArgs e)
		{
			ThreeDFloorHelperControl ctrl = GetThreeDFloorControl();

			ctrl.Show();

			threeDFloorPanel.ScrollControlIntoView(ctrl);

			no3dfloorspanel.Hide();
		}

		public void DuplicateThreeDFloor(ThreeDFloorHelperControl ctrl)
		{
			ThreeDFloorHelperControl dup = GetThreeDFloorControl();

			dup.Update(ctrl);
			ctrl.Show();

			threeDFloorPanel.ScrollControlIntoView(dup);
		}

		public void SplitThreeDFloor(ThreeDFloorHelperControl ctrl)
		{
			var items = new List<int>();
			var controls = new List<ThreeDFloorHelperControl>();
			int startat = 1;
			int numsplits = 0;

			for (int i = 0; i < ctrl.checkedListBoxSectors.Items.Count; i++)
			{
				if(ctrl.checkedListBoxSectors.GetItemCheckState(i) == CheckState.Checked)
					items.Add(i);
			}

			int useitem = items.Count - 1;

			/*
			Case 1: all tagged sectors are also selected sectors. In this case we can reuse
				the original control, so one less additional control is needed
			Case 2: multiple tagged sectors are also selected sectors. In this case we can
				reuse the original control, so one less additional control is needed
			Case 3: only one tagged sector is also the selected sector. In this case we
				have to add exactly one additional control
			*/

			controls.Add(ctrl);

			if (items.Count == 1)
			{
				numsplits = 1;
			}
			else
			{
				numsplits = items.Count - 1;
			}

			if (items.Count == 1)
				startat = 0;

			for (int i = 0; i < numsplits; i++)
			{
				var newctrl = GetThreeDFloorControl();

				newctrl.Update(ctrl);
				newctrl.Show();

				controls.Add(newctrl);
			}

			for (int i = controls.Count - 1; i >= 0 ; i--)
			{
				for (int j = 0; j < items.Count; j++)
				{
					controls[i].checkedListBoxSectors.SetItemChecked(j, false);
				}

				if (useitem >= 0)
					controls[i].checkedListBoxSectors.SetItemChecked(items[useitem], true);

				useitem--;
			}
		}

		private void FillThreeDFloorPanel(List<ThreeDFloor> threedfloors)
		{
			if (threedfloors.Count > 0)
			{
				// Create a new controller instance for each linedef and set its properties
				foreach (ThreeDFloor tdf in threedfloors.OrderByDescending(o => o.TopHeight).ToList())
				{
					ThreeDFloorHelperControl ctrl = GetThreeDFloorControl();
					ctrl.Update(tdf);
					ctrl.Show();
				}

				no3dfloorspanel.Hide();
			}
			else
			{
				no3dfloorspanel.Show();
			}

			// Hide all unused pool controls
			if (controlpool.Count - threedfloors.Count > 0)
			{
				foreach (ThreeDFloorHelperControl ctrl in controlpool.Skip(threedfloors.Count))
				{
					ctrl.Used = false;
					ctrl.Hide();
				}
			}
		}

		private void sharedThreeDFloorsCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			ICollection<Sector> selectedSectors = General.Map.Map.GetSelectedSectors(true);

			if (selectedSectors.Count > 1 && sharedThreeDFloorsCheckBox.Checked)
			{
				var hideControls = new List<ThreeDFloorHelperControl>();

				foreach (Sector s in selectedSectors)
				{
					foreach (ThreeDFloorHelperControl ctrl in threeDFloorPanel.Controls.OfType<ThreeDFloorHelperControl>())
					{
						// If the selected sector is not in the control's tagged sectors the control
						// should be hidden
						if (!ctrl.ThreeDFloor.TaggedSectors.Contains(s))
							hideControls.Add(ctrl);
					}
				}

				foreach (ThreeDFloorHelperControl ctrl in hideControls)
				{
					// Hide controls, unless they are new
					if (ctrl.IsNew == false)
						ctrl.Hide();
				}
			}
			else
			{
				foreach (ThreeDFloorHelperControl ctrl in threeDFloorPanel.Controls.OfType<ThreeDFloorHelperControl>())
					if (ctrl.Used)
						ctrl.Show();
			}
		}

		private void ThreeDFloorEditorWindow_FormClosed(object sender, FormClosedEventArgs e)
		{
			// Get rid of dummy sectors and makes all controls available
			foreach (ThreeDFloorHelperControl ctrl in threeDFloorPanel.Controls.OfType<ThreeDFloorHelperControl>().ToList())
			{
				if (ctrl.Sector != null)
					ctrl.Sector.Dispose();

				ctrl.Used = false;
			}

			General.Map.Map.Update();
		}
	}
}
