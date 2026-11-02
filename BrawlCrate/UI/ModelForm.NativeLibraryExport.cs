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
        private void ChooseOriginalNativeModelExport()
        {
            if(SimpleWorkspaceBusy)throw new InvalidOperationException("Finish or cancel the current operation first.");
            if(_fighterProject.NativeTarget==null||!(NativeGameAdapters.ForNative(_fighterProject.NativeTarget.Provider) is INativeTargetModelSourceExportAdapter))
                throw new NotSupportedException("This target format has no qualified original native model export.");
            using(var folder=new FolderBrowserDialog{Description="Choose a separate parent folder. Fighterport will create a new folder with the original native costume DAT, embedded textures and fighter context."})
            {
                if(folder.ShowDialog(this)!=DialogResult.OK)return;
                string destination=Path.Combine(folder.SelectedPath,"Fighterport-native-model-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
                ExportOriginalNativeModelBundle(destination);
                _fighterStatus.Text="Exported original native model and embedded textures to "+destination+". Target animation unchanged.";
            }
        }
        internal void ExportOriginalNativeModelBundle(string destination)
        {
            if(SimpleWorkspaceBusy||_pinGesture!=null||modelEditControl1.AwaitingRedoSave)throw new InvalidOperationException("Finish the current native operation before exporting.");
            var project=_fighterProject;
            var target=modelEditControl1.TargetModel as BrawlLib.SSBB.ResourceNodes.MDL0Node;
            if(project?.NativeTarget==null||target==null||!OwnsFighterRoot(target))throw new InvalidOperationException("Open a project-owned native target model first.");
            if(target.IsDirty)throw new NotSupportedException("Edited native model writing is not qualified. Save or undo model edits before exporting the original costume.");
            var adapter=NativeGameAdapters.ForNative(project.NativeTarget.Provider) as INativeTargetModelSourceExportAdapter
                ??throw new NotSupportedException("This target format has no qualified original native model export.");
            string dependency=NativeTargetBinding.DependencyKey(project);
            void Current()
            {
                if(!ReferenceEquals(project,_fighterProject)||!ReferenceEquals(target,modelEditControl1.TargetModel)||target.IsDirty||NativeTargetBinding.DependencyKey(project)!=dependency)
                    throw new InvalidOperationException("Native target or source dependencies changed during model export.");
            }
            ProtectCharacterWorkspaceOutput(destination);_library?.ProtectOutputPath(destination);
            adapter.ExportOriginalModel(project,destination,System.Threading.CancellationToken.None,Current);
        }
    }
}
