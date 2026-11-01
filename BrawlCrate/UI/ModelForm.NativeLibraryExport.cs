using BrawlCrate.Studio;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private void ExportOriginalLibraryFiles()
        {
            if(SimpleWorkspaceBusy)throw new InvalidOperationException("Finish or cancel the current operation first.");
            var item=LibrarySelection??throw new InvalidOperationException("Select an exact native library animation first.");
            var adapter=NativeGameAdapters.ForNative(item.Motion.Provider) as INativeSourceExportAdapter??throw new NotSupportedException("This adapter does not qualify native source-file export.");
            var data=_library.Read();var clip=data.Clips.Single(c=>c.Id==item.Clip.Id);string modelId=SelectedLibraryCostume?.ModelId??clip.ModelId;
            var model=data.Models.Single(m=>m.Id==modelId);
            using(var folder=new FolderBrowserDialog{Description="Choose a separate parent folder. Fighterport will create a new folder containing the unchanged native costume and motion files."})
            {
                if(folder.ShowDialog(this)!=DialogResult.OK)return;
                string destination=Path.Combine(folder.SelectedPath,"Fighterport-native-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+clip.Id.Substring(0,8));
                _library.ProtectOutputPath(destination);PauseSourceOperation();
                var exported=LibraryWork.Run(this,"Exporting original native files",(cancel,progress)=>{progress.Report("Verifying original native costume/motion bytes; the edited target is not exported.");adapter.ExportOriginal(data,clip,model,destination,cancel);return destination;});
                if(exported!=null)_fighterStatus.Text="Original native source files exported to "+exported+". Target animation unchanged.";
            }
        }
    }
}
