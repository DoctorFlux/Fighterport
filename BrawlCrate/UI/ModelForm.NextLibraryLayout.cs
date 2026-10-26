using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private TableLayoutPanel _sourceCatalogueLayout,_sourcePreviewHost;
        private Panel _sourceCatalogueScroll;
        private TableLayoutPanel _sourcePreviewTransport;
        private Button _sourceFitButton;
        private int _sourceTransportText=-1, _sourcePauseWidth;

        private void BuildNextLibraryPreviewHost(TableLayoutPanel catalogue,TableLayoutPanel transport)
        {
            _sourceCatalogueScroll=new Panel{Name="libraryCatalogueScroll",Dock=DockStyle.Fill,AutoScroll=true};
            _sourcePreviewHost=new TableLayoutPanel{Name="librarySourcePreviewHost",Dock=DockStyle.Bottom,ColumnCount=1,RowCount=4,Margin=Padding.Empty};
            _sourcePreviewHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            _sourcePreviewHost.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
            _sourcePreviewHost.RowStyles.Add(new RowStyle(SizeType.Absolute,190));
            _sourcePreviewHost.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            _sourcePreviewHost.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            _sourcePreviewTransport=transport;
            foreach(Control control in new Control[]{_libraryOriginalLabel,_libraryViewport,transport})catalogue.Controls.Remove(control);
            _sourcePreviewHost.Controls.Add(_libraryOriginalLabel,0,0);
            _sourcePreviewHost.Controls.Add(_libraryViewport,0,1);
            _sourcePreviewHost.Controls.Add(transport,0,2);
            // Keep the two everyday operations visible beside the target even
            // when large text makes the catalogue above need scrolling.
            var actions=new TableLayoutPanel{Name="sourcePrimaryActions",Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
            _libraryUse.AutoSize=false;_libraryUse.Dock=DockStyle.Fill;
            _sourceFitButton=LibraryButton("sourceFitTarget","Fit start / end...",ShowTransitionFit);
            _sourceFitButton.AutoSize=false;_sourceFitButton.Dock=DockStyle.Fill;
            _sourceFitButton.AccessibleDescription="Fit the selected target animation to chosen start or end poses. Each transition can be switched on or off.";
            actions.Controls.Add(_libraryUse,0,0);actions.Controls.Add(_sourceFitButton,1,0);
            _sourcePreviewHost.Controls.Add(actions,0,3);
            _libraryPlay.AutoSize=false;_libraryPlay.Dock=DockStyle.Fill;
            for(int row=6;row<9;row++)catalogue.RowStyles[row].Height=0;
            _sourceCatalogueScroll.Controls.Add(catalogue);
            _libraryPane.Controls.Add(_sourceCatalogueScroll);_libraryPane.Controls.Add(_sourcePreviewHost);
        }

        private void LayoutNextLibraryPreview()
        {
            if (_libraryPane == null) return;
            var layout = _sourceCatalogueLayout;
            if (layout == null) return;

            // Reuse the width/text/caption measurement cache. Ordinary status
            // updates only reconcile these cheap row allocations, never load a
            // source or touch the native target's scene and document ownership.
            int line = WorkspaceAppearance.Font.Height;
            if(_sourceModeLabel!=null)
            {
                _sourceModeLabel.Width=Math.Max(1,_libraryPane.ClientSize.Width-_libraryPane.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-8);
                _sourceModeLabel.Height=line+8;
            }
            ApplyWorkspaceLibraryTextSize(layout);
            _libraryPane.AutoScroll=false;
            layout.RowStyles[4].Height = Math.Max(line * 3 + 8, WorkspaceAppearance.Pixels(48));
            _sourcePreviewHost.RowStyles[0].Height = Math.Max(line + 8, WorkspaceAppearance.Pixels(24));
            // The preview is a small native viewport, independent of text size.
            // Retain its place before explicit source loading rather than
            // shifting the catalogue and transport after every Preview action.
            _sourcePreviewHost.RowStyles[1].Height = 190;
            _sourcePreviewHost.RowStyles[2].Height = Math.Max(line + 14, WorkspaceAppearance.Pixels(38));
            _sourcePreviewHost.RowStyles[3].Height = Math.Max(line + 16, WorkspaceAppearance.Pixels(38));
            _sourcePreviewHost.Height=(int)_sourcePreviewHost.RowStyles.Cast<RowStyle>().Sum(row=>row.Height);
            if (_sourceTransportText != WorkspaceAppearance.TextPercent)
            {
                _sourceTransportText=WorkspaceAppearance.TextPercent;
                _sourcePauseWidth=TextRenderer.MeasureText("Pause",WorkspaceAppearance.Font).Width+24;
            }
            _sourcePreviewTransport.ColumnStyles[0].Width=Math.Max(WorkspaceAppearance.Pixels(72),_sourcePauseWidth);

            bool loaded = _libraryLease != null;
            _libraryPlay.Enabled = _libraryFrame.Enabled = loaded && !SimpleWorkspaceBusy;
            if (!loaded)
                _libraryOriginalLabel.Text = "Original source preview · choose Preview Original (read-only)";

            // A useful catalogue and the fixed source viewport remain available
            // at normal sizes. Small windows or large text scroll the one
            // library pane instead of collapsing the source preview to zero.
            int catalogue = Math.Max(140, line * 8) + _libraryList.Margin.Vertical;
            int fixedRows = (int)layout.RowStyles.Cast<RowStyle>()
                .Where(row => row.SizeType == SizeType.Absolute).Sum(row => row.Height);
            layout.Height = Math.Max(_sourceCatalogueScroll.ClientSize.Height,
                fixedRows + catalogue);
        }
    }
}
