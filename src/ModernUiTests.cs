using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ScopeMaker {
public partial class MainWindow {
 static IEnumerable<Control> Descendants(Control control){foreach(Control c in control.Controls){yield return c;foreach(var child in Descendants(c))yield return child;}}
 static void AssertAccessible(Control control){if(!control.Visible)throw new Exception("UI action is hidden: "+control.Text);Control child=control;for(Control parent=control.Parent;parent!=null;parent=parent.Parent){Rectangle visible=parent.RectangleToClient(child.RectangleToScreen(child.ClientRectangle));if(visible.Left< -2||visible.Top< -2||visible.Right>parent.ClientSize.Width+2||visible.Bottom>parent.ClientSize.Height+2)throw new Exception("UI action clipped: "+control.Text+" / "+((control as ModernButton)==null?"":((ModernButton)control).Glyph)+" in "+parent.GetType().Name+" "+visible+" / "+parent.ClientSize);child=parent;}}
 public static void ModernChecks(List<string> results){using(var w=new MainWindow()){
  w.Show();Application.DoEvents();if(!System.Windows.Forms.Screen.FromControl(w).WorkingArea.Contains(w.Bounds))throw new Exception("Initial window exceeds the working area");results.Add("PASS: Initial window fits the desktop working area");
  foreach(var size in new[]{new Size(1440,900),new Size(1204,721)}){w.ClientSize=size;Application.DoEvents();foreach(var b in Descendants(w).OfType<ModernButton>().Where(b=>b.Primary||b.Glyph=="copy"||b.Glyph=="delete"||b==w.elementTab||b==w.templateTab))AssertAccessible(b);if(w.canvas.Width<500||w.canvas.Height<400)throw new Exception("Canvas too small at minimum window size");}
  results.Add("PASS: Main actions and sidebar tabs remain accessible at default and minimum window size");
  w.ShowSidebar(true);w.templateSearch.Text="leclerc";if(w.templateList.Items.Count==0||w.templateList.Items.Cast<string>().Any(s=>s.IndexOf("leclerc",StringComparison.OrdinalIgnoreCase)<0))throw new Exception("Template search is incorrect");w.ShowSidebar(false);if(!w.layerPane.Visible||w.templatePane.Visible)throw new Exception("Sidebar tabs failed");results.Add("PASS: Template search and switching back to layers work");
  if(w.text.Visible||w.numbers["Size"].Visible||!w.strokeWidth.Visible)throw new Exception("Line inspector shows irrelevant controls");w.AddPart("text");if(!w.text.Visible||!w.textAlign.Visible||w.strokeWidth.Visible||w.numbers["X2"].Visible)throw new Exception("Text inspector shows irrelevant controls");w.advancedButton.PerformClick();if(!w.angularUnits.Visible||!w.mils.Visible)throw new Exception("Advanced WT settings are not accessible");w.advancedButton.PerformClick();if(w.angularUnits.Visible||w.mils.Visible)throw new Exception("Advanced WT settings failed to collapse");results.Add("PASS: Inspector shows relevant fields and advanced WT settings expand and collapse");
  w.ModernShortcuts(w,new KeyEventArgs(Keys.L));if(w.drawingTool.SelectedIndex!=1)throw new Exception("Line shortcut failed");w.dirty=false;w.Close();
 }}
}
}
