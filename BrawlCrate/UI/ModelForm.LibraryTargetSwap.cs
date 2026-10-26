using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Linq;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        internal bool ReplaceLibraryTarget(FighterProject candidate)
        {
            ValidateFighterProjectAssets(candidate);
            if(!PrepareFighterDocumentChange(false))return false;
            FinishControlCurveEdit();CancelLibraryAutoPreview();
            var previous=_fighterProject;var roots=_fighterRoots.ToArray();var assets=_fighterAssets.ToArray();
            var undo=modelEditControl1._undoSaves.ToArray();var redo=modelEditControl1._redoSaves.ToArray();int cursor=modelEditControl1._saveIndex;
            bool undoing=modelEditControl1._undoing;var pending=modelEditControl1._currentUndo;
            bool dirty=_fighterDirty;string bone=modelEditControl1.SelectedBone?.Name;int frame=modelEditControl1.CurrentFrame;
            var programRoot=Program.RootNode;string programPath=Program._rootPath;
            try
            {
                if(roots.Any(p=>ReferenceEquals(p.Value,Program.RootNode?.RootNode))){Program.RootNode=null;Program._rootPath=null;}
                _fighterProject=candidate;_fighterRoots.Clear();_fighterAssets.Clear();
                RebuildFighterScene();
            }
            catch
            {
                foreach(var root in _fighterRoots.Values.Distinct())root.Dispose();
                _fighterRoots.Clear();foreach(var item in roots)_fighterRoots.Add(item.Key,item.Value);
                _fighterAssets.Clear();foreach(var item in assets)_fighterAssets.Add(item.Key,item.Value);
                _fighterProject=previous;RebuildFighterScene();modelEditControl1.SetFrame(frame);
                if(bone!=null&&modelEditControl1.TargetModel is MDL0Node oldModel)modelEditControl1.SelectedBone=oldModel.AllBones.FirstOrDefault(b=>b.Name==bone);
                modelEditControl1._undoSaves.Clear();modelEditControl1._undoSaves.AddRange(undo);modelEditControl1._redoSaves.Clear();modelEditControl1._redoSaves.AddRange(redo);modelEditControl1._saveIndex=cursor;modelEditControl1._undoing=undoing;modelEditControl1._currentUndo=pending;modelEditControl1.UpdateUndoButtons();
                Program.RootNode=programRoot;Program._rootPath=programPath;_fighterDirty=dirty;throw;
            }
            CloseLinkedComparison();ResetLoopInspection();ResetBatchReview();
            _fighterImportUndo.Clear();_workspaceSceneId=_workspaceModelId=null;_workspaceSavedAssets=null;
            _curveTarget=null;_curveBefore=null;_curvePending=false;
            _fighterName.Text=candidate.Name;_fighterDirty=true;
            modelEditControl1._undoSaves.Clear();modelEditControl1._redoSaves.Clear();modelEditControl1._saveIndex=0;modelEditControl1._undoing=false;modelEditControl1._currentUndo=null;modelEditControl1.UpdateUndoButtons();
            foreach(var root in roots.Select(p=>p.Value).Distinct())root.Dispose();previous.Dispose();
            _fighterClosingRoots=null;InvalidateControls();modelEditControl1.QueueFrameFighterModel();
            UpdateFighterStatus();return true;
        }
    }
}
