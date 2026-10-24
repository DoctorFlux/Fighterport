using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    // Recolor original controls without changing their bounds, padding or fonts.
    internal static class DarkTheme
    {
        internal static readonly Color Surface=Color.FromArgb(31,33,38), Field=Color.FromArgb(22,24,28), Text=Color.FromArgb(229,231,235);
        private static readonly ConditionalWeakTable<Control,object> Applied=new ConditionalWeakTable<Control,object>();
        [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd,string app,string id);
        [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        internal static void Install(){Application.Idle+=(s,e)=>{foreach(Form form in Application.OpenForms)Apply(form);};}
        internal static void Apply(Control control)
        {
            if(Applied.TryGetValue(control,out _))return;Applied.Add(control,new object());
            BrawlLib.Internal.Windows.Controls.NumericInputBox.KeyframeHighlightColor=Color.FromArgb(90,73,24);
            control.BackColor=control is TextBoxBase||control is ListControl||control is ListView||control is TreeView||control is NumericUpDown?Field:Surface;
            if(control.ForeColor!=Color.Gray)control.ForeColor=Text;
            control.BackColorChanged+=(s,e)=>{var c=control.BackColor;if(c.R+c.G+c.B>440&&c.A>0)control.BackColor=Field;};
            control.ForeColorChanged+=(s,e)=>{if(control.ForeColor==Color.Black||control.ForeColor==SystemColors.ControlText||control.ForeColor==SystemColors.WindowText)control.ForeColor=Text;};
            if(control is Button button){button.UseVisualStyleBackColor=false;button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderColor=Color.FromArgb(78,82,92);}
            if(control is ToolStrip strip){strip.Renderer=new ToolStripProfessionalRenderer(new Palette());foreach(ToolStripItem item in strip.Items)ApplyItem(item);strip.ItemAdded+=(s,e)=>ApplyItem(e.Item);}
            if(control is TreeView tree)tree.LineColor=Color.FromArgb(83,88,100);
            if(control is ComboBox combo&&combo.DrawMode==DrawMode.Normal){combo.FlatStyle=FlatStyle.Flat;combo.DrawMode=DrawMode.OwnerDrawFixed;combo.DrawItem+=(s,e)=>{if(e.Index<0)return;using(var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(57,67,84):Field))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,combo.GetItemText(combo.Items[e.Index]),e.Font,e.Bounds,Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);};}
            if(control is ListView list&&!list.OwnerDraw){list.OwnerDraw=true;list.DrawColumnHeader+=(s,e)=>{using(var brush=new SolidBrush(Surface))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,list.Font,e.Bounds,Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);};list.DrawItem+=(s,e)=>{if(list.View!=View.Details)e.DrawDefault=true;};list.DrawSubItem+=(s,e)=>{using(var brush=new SolidBrush(e.Item.Selected?Color.FromArgb(57,67,84):Field))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.SubItem.Text,list.Font,e.Bounds,Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);};}
            if(control is PropertyGrid grid){grid.ViewBackColor=Field;grid.ViewForeColor=Text;grid.HelpBackColor=Surface;grid.HelpForeColor=Text;grid.CategoryForeColor=Text;grid.CommandsBackColor=Surface;grid.CommandsForeColor=Text;}
            if(control is DataGridView table)
            {
                table.EnableHeadersVisualStyles=false;table.BackgroundColor=Field;table.GridColor=Color.FromArgb(78,82,92);
                table.DefaultCellStyle.BackColor=Field;table.DefaultCellStyle.ForeColor=Text;table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(57,67,84);table.DefaultCellStyle.SelectionForeColor=Text;
                table.ColumnHeadersDefaultCellStyle.BackColor=Surface;table.ColumnHeadersDefaultCellStyle.ForeColor=Text;table.RowHeadersDefaultCellStyle.BackColor=Surface;table.RowHeadersDefaultCellStyle.ForeColor=Text;
                foreach(DataGridViewColumn column in table.Columns)if(column is DataGridViewComboBoxColumn choice)choice.FlatStyle=FlatStyle.Flat;
                table.ColumnAdded+=(s,e)=>{if(e.Column is DataGridViewComboBoxColumn choice)choice.FlatStyle=FlatStyle.Flat;};
                table.EditingControlShowing+=(s,e)=>Apply(e.Control);
            }
            if(control is TabControl tabs){tabs.DrawMode=TabDrawMode.OwnerDrawFixed;tabs.DrawItem+=(s,e)=>{using(var brush=new SolidBrush(Surface))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,tabs.TabPages[e.Index].Text,tabs.Font,e.Bounds,Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);};}
            control.HandleCreated+=(s,e)=>Native(control);if(control.IsHandleCreated)Native(control);
            control.ControlAdded+=(s,e)=>Apply(e.Control);foreach(Control child in control.Controls)Apply(child);
        }
        private static void Native(Control c){if(Environment.OSVersion.Platform==PlatformID.Win32NT)SetWindowTheme(c.Handle,"DarkMode_Explorer",null);if(c is Form){int on=1;DwmSetWindowAttribute(c.Handle,20,ref on,4);}}
        private static void ApplyItem(ToolStripItem item)
        {
            item.BackColor=Surface;item.ForeColor=Text;
            if(item is ToolStripControlHost host)Apply(host.Control);
            if(item is ToolStripDropDownItem drop){drop.DropDown.BackColor=Surface;drop.DropDown.Renderer=new ToolStripProfessionalRenderer(new Palette());foreach(ToolStripItem child in drop.DropDownItems)ApplyItem(child);drop.DropDown.ItemAdded+=(s,e)=>ApplyItem(e.Item);}
        }
        private sealed class Palette:ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin=>Surface;public override Color ToolStripGradientMiddle=>Surface;public override Color ToolStripGradientEnd=>Surface;
            public override Color MenuStripGradientBegin=>Surface;public override Color MenuStripGradientEnd=>Surface;
            public override Color ToolStripDropDownBackground=>Surface;public override Color ImageMarginGradientBegin=>Surface;public override Color ImageMarginGradientMiddle=>Surface;public override Color ImageMarginGradientEnd=>Surface;
            public override Color MenuItemSelected=>Color.FromArgb(57,67,84);public override Color MenuItemSelectedGradientBegin=>MenuItemSelected;public override Color MenuItemSelectedGradientEnd=>MenuItemSelected;
            public override Color MenuItemPressedGradientBegin=>MenuItemSelected;public override Color MenuItemPressedGradientMiddle=>MenuItemSelected;public override Color MenuItemPressedGradientEnd=>MenuItemSelected;
            public override Color ButtonSelectedGradientBegin=>MenuItemSelected;public override Color ButtonSelectedGradientMiddle=>MenuItemSelected;public override Color ButtonSelectedGradientEnd=>MenuItemSelected;
            public override Color MenuBorder=>Color.FromArgb(78,82,92);public override Color SeparatorDark=>MenuBorder;public override Color SeparatorLight=>Surface;
        }
    }
}
