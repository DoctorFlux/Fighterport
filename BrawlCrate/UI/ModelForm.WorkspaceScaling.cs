using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private int NativeWorkspaceMinimumWidth => Math.Max(760,modelEditControl1.CHR0Editor.MinimumSize.Width+modelEditControl1.PlaybackPanel.MinimumSize.Width+16);

        private void ToggleSimpleWorkspaceWorkflow()
        {
            if (SimpleWorkspaceBusy) return;
            bool shown=_simpleWorkspaceTask.Visible;
            HideBatchReview(); HideLoopInspection();
            if (_comparisonPane != null) _comparisonPane.Visible = false;
            if (!shown) _workspaceTaskOpener = _workspaceInvoker ?? CurrentWorkspaceFocus() ?? _simpleWorkspaceWorkflow;
            _simpleWorkspaceFocused=shown;
            _simpleWorkspaceDrawer=!shown;
            if(!shown && _libraryPane?.Visible==true)SetSimpleWorkspaceLibraryVisible(false);
            UpdateSimpleWorkspace();
            if (shown) RestoreWorkspaceFocus(_workspaceTaskOpener);
            else FocusWorkspaceRegion(_simpleWorkspaceTask, remember: false);
        }

        private void SetWorkspaceTextSize(int percent)
        {
            if(SimpleWorkspaceBusy)return;
            string preferenceError=WorkspaceAppearance.SetTextPercent(percent);
            ApplyWorkspaceTextSize();
            UpdateSimpleWorkspace();
            if(preferenceError!=null)_fighterStatus.Text=preferenceError;
        }

        private void ApplyWorkspaceTextSize()
        {
            // Only the outer workflow surfaces inherit this presentation font.
            // Never scale/reparent the native editor's internal controls.
            foreach(var bar in new[]{_simpleWorkspaceToolbar,_simpleWorkspaceNavigation,_simpleWorkspaceActions})
                if(bar!=null)bar.Font=WorkspaceAppearance.Font;
            _fighterName.Font=WorkspaceAppearance.Font;
            _simpleWorkspaceNavigation.Height=WorkspaceAppearance.Font.Height+WorkspaceAppearance.Pixels(16);
            _simpleWorkspaceTask.ApplyTextSize();
            foreach(ToolStripMenuItem item in _simpleWorkspaceOptions.DropDownItems)
                item.Checked=(int)item.Tag==WorkspaceAppearance.TextPercent;
            _simpleWorkspaceOptions.Text="Text: "+WorkspaceAppearance.TextPercent+"%";
            LayoutSimpleWorkspace();
        }

        private void LayoutWorkspaceBars()
        {
            int width=ClientSize.Width;
            _fighterName.Width=Math.Min(WorkspaceAppearance.Pixels(145),Math.Max(140,width/5));
            var operation=_simpleWorkspaceToolbar.Items["workspaceOperation"];
            var game=_simpleWorkspaceToolbar.Items["workspaceTargetGame"];
            operation.Available=game.Available=true;
            int requested=_simpleWorkspaceToolbar.Items.Cast<ToolStripItem>()
                .Where(i=>i.Available && i.Overflow!=ToolStripItemOverflow.Always)
                .Sum(i=>i.GetPreferredSize(Size.Empty).Width+i.Margin.Horizontal)+40;
            if(requested>width)operation.Available=false;
            if(requested-operation.GetPreferredSize(Size.Empty).Width>width)game.Available=false;

            var commands=new[]{_simpleWorkspaceWorkflow,_simpleWorkspaceLibrary,_simpleWorkspaceFocus,
                _simpleWorkspaceMoreTools,_simpleWorkspaceOpenProject,_simpleWorkspaceNewProject};
            string[] full={"Workflow","Source library",_simpleWorkspaceFocused?"Show workflow":"Focus editor","More tools","Open project","New project"};
            string[] compact={"Workflow","Library",_simpleWorkspaceFocused?"Show tasks":"Focus","Tools","Open","New"};
            for(int i=0;i<commands.Length;i++)
            {
                commands[i].Text=full[i];commands[i].AccessibleName=full[i];commands[i].ToolTipText=full[i]+". F6 or Shift+F6 moves between workspace regions.";
                commands[i].Overflow=ToolStripItemOverflow.Never;
            }
            int actionWidth=_simpleWorkspaceActions.Items.Cast<ToolStripItem>().Sum(i=>i.GetPreferredSize(Size.Empty).Width+i.Margin.Horizontal)+16;
            if(actionWidth>width)for(int i=0;i<commands.Length;i++)commands[i].Text=compact[i];
            int stageWidth=_simpleWorkspaceStages.Sum(i=>i.GetPreferredSize(Size.Empty).Width+i.Margin.Horizontal)+20;
            _simpleWorkspaceSummary.AutoSize=false;
            _simpleWorkspaceSummary.Width=Math.Max(0,width-stageWidth);
            _simpleWorkspaceSummary.Height=WorkspaceAppearance.Font.Height+WorkspaceAppearance.Pixels(10);
            _simpleWorkspaceSummary.ToolTipText=_simpleWorkspaceSummary.Text;
            _simpleWorkspaceSummary.Available=width-stageWidth>=180;
        }

        private void ApplyWorkspaceLibraryTextSize(TableLayoutPanel layout)
        {
            if(_libraryPane==null)return;
            _libraryPane.Font=WorkspaceAppearance.Font;
            _libraryPane.AutoScroll=true;
            layout.Dock=DockStyle.Top;
            int line=WorkspaceAppearance.Font.Height;
            foreach(int row in new[]{0,5})
            {
                var flow=layout.GetControlFromPosition(0,row) as FlowLayoutPanel;
                if(flow==null)continue;
                foreach(Button button in flow.Controls.OfType<Button>())
                {
                    button.AutoSize=false;
                    button.Size=new Size(TextRenderer.MeasureText(button.Text,button.Font).Width+WorkspaceAppearance.Pixels(16),Math.Max(line+12,WorkspaceAppearance.Pixels(30)));
                }
                int available=Math.Max(1,_libraryPane.ClientSize.Width-_libraryPane.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-8);
                int rows=1,used=0,rowHeight=0;
                foreach(Control control in flow.Controls)
                {
                    int itemWidth=control.Width+control.Margin.Horizontal;
                    if(used>0 && used+itemWidth>available){rows++;used=0;}
                    used+=itemWidth;rowHeight=Math.Max(rowHeight,control.Height+control.Margin.Vertical);
                }
                layout.RowStyles[row].Height=rows*rowHeight+4;
            }
            SizeLibraryFilters(line);
            layout.RowStyles[3].Height=line+12;
            var search=_libraryQueryStack.Controls.Find("librarySearchRow",true).Single();
            foreach(Control label in search.Controls)
                if(label!=_librarySearch)label.Width=TextRenderer.MeasureText(label.Text,label.Font).Width+WorkspaceAppearance.Pixels(label is Button?20:8);
            _libraryList.ItemHeight=line+WorkspaceAppearance.Pixels(5);
            var transport=layout.GetControlFromPosition(0,8) as TableLayoutPanel;
            if(transport!=null)transport.ColumnStyles[0].Width=WorkspaceAppearance.Pixels(72);
        }
    }
}
