using BrawlCrate.Studio;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    // One association review for an entire source motion set, never per use.
    internal sealed class LibrarySourceDialog : Form
    {
        private readonly ComboBox modelFile=new ComboBox(),motionFile=new ComboBox(),model=new ComboBox(),game=new ComboBox();
        private readonly TextBox character=new TextBox(),version=new TextBox();
        private readonly CheckBox approve=new CheckBox();
        private readonly Label identity=new Label();
        private readonly Button add=new Button();
        private List<LibraryModel> models=new List<LibraryModel>();
        internal LibraryRegistration Registration {get;private set;}
        internal LibrarySourceDialog()
        {
            Text="Add Source — Whole Character Motion Set";Name="LibrarySourceDialog";ClientSize=new Size(620,372);
            FormBorderStyle=FormBorderStyle.FixedDialog;StartPosition=FormStartPosition.CenterParent;MaximizeBox=MinimizeBox=false;ShowInTaskbar=false;
            var folder=new Button{Name="librarySourceFolder",Text="From Folder...",Bounds=new Rectangle(472,12,132,28)};
            folder.Click+=(s,e)=>ChooseFolder();
            Controls.AddRange(new Control[]{new Label{Text="Read-only Brawl sources. Select the authentic model once for this motion set.",Bounds=new Rectangle(14,15,452,32)},folder});
            SetupPath(modelFile,"libraryModelFile",48,()=>ChooseFile(modelFile,"Choose Original Source Model","Brawl models|*.pac;*.pcs;*.brres;*.mdl0"));
            SetupPath(motionFile,"libraryMotionFile",86,()=>ChooseFile(motionFile,"Choose Original Motion Set","Brawl motions|*.pac;*.pcs;*.brres;*.chr0"));
            Controls.Add(new Label{Text="Original model",Bounds=new Rectangle(14,128,110,20)});
            model.Name="librarySourceModel";model.AccessibleName="Original source model";model.Bounds=new Rectangle(132,124,472,26);model.DropDownStyle=ComboBoxStyle.DropDownList;
            model.SelectedIndexChanged+=(s,e)=>UpdateSelectedModel();
            Controls.Add(model);identity.Bounds=new Rectangle(132,153,472,22);Controls.Add(identity);
            Controls.Add(new Label{Text="Character label",Bounds=new Rectangle(14,185,110,20)});
            character.Name="libraryCharacter";character.AccessibleName="Source character label";character.Bounds=new Rectangle(132,180,472,25);character.MaxLength=120;Controls.Add(character);
            Controls.Add(new Label{Text="Game / build",Bounds=new Rectangle(14,221,110,20)});
            game.Name="librarySourceGame";game.Bounds=new Rectangle(132,216,118,26);game.DropDownStyle=ComboBoxStyle.DropDownList;
            game.Items.AddRange(new object[]{"Brawl","Project M","Project+"});game.SelectedIndex=0;
            version.Name="libraryGameVersion";version.AccessibleName="Source game version";version.Bounds=new Rectangle(258,216,346,26);version.MaxLength=240;version.Text="Build not supplied";
            Controls.AddRange(new Control[]{game,version,new Label{Text="PACs encode native resource versions, not an authoritative game build. Enter the known region/revision or keep it explicitly unknown.",Bounds=new Rectangle(132,246,472,36)}});
            approve.Name="libraryApproveAssociation";approve.AccessibleName="Approve original model and motion association";
            approve.Text="I confirm these body motions belong to this exact original model.";approve.Bounds=new Rectangle(14,286,590,25);
            Controls.Add(approve);
            add.Name="libraryRegisterSource";add.Text="Add Whole Motion Set";add.Bounds=new Rectangle(318,330,174,28);add.Click+=(s,e)=>Accept();
            var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(504,330,100,28)};Controls.AddRange(new Control[]{add,cancel});CancelButton=cancel;
            modelFile.SelectedIndexChanged+=(s,e)=>InspectModelFile();
        }
        private void SetupPath(ComboBox box,string name,int y,Action choose)
        {
            box.Name=name;box.AccessibleName=name=="libraryModelFile"?"Original model file":"Original motion file";
            box.Bounds=new Rectangle(132,y,388,27);box.DropDownStyle=ComboBoxStyle.DropDownList;box.DropDownWidth=740;
            var browse=new Button{Name=name+"Browse",Text="Browse...",Bounds=new Rectangle(528,y,76,27)};browse.Click+=(s,e)=>choose();
            Controls.AddRange(new Control[]{new Label{Text=name=="libraryModelFile"?"Model package":"Motion package",Bounds=new Rectangle(14,y+5,112,22)},box,browse});
        }
        private void ChooseFile(ComboBox box,string title,string filter)
        {
            using(var dialog=new OpenFileDialog{Title=title,Filter=filter})if(dialog.ShowDialog(this)==DialogResult.OK)
            {if(!box.Items.Contains(dialog.FileName))box.Items.Add(dialog.FileName);box.SelectedItem=dialog.FileName;}
        }
        private void ChooseFolder()
        {
            using(var dialog=new FolderBrowserDialog{Description="Choose an extracted source folder (no files will be changed)"})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                // Loose-folder inventory is intentionally bounded and shallow.
                var files=Directory.EnumerateFiles(dialog.SelectedPath).Where(p=>new[]{".pac",".pcs",".brres",".mdl0",".chr0"}.Contains(Path.GetExtension(p).ToLowerInvariant())).Take(129).ToArray();
                if(files.Length>128){MessageBox.Show(this,"Choose model and motion files directly; this folder exceeds the 128-file discovery limit.");return;}
                foreach(string path in files)
                {
                    if(Path.GetExtension(path).ToLowerInvariant()!=".chr0"&&!modelFile.Items.Contains(path))modelFile.Items.Add(path);
                    if(Path.GetExtension(path).ToLowerInvariant()!=".mdl0"&&!motionFile.Items.Contains(path))motionFile.Items.Add(path);
                }
                identity.Text="Select the actual model and motion packages; filenames do not approve ownership.";
            }
        }
        private void InspectModelFile()
        {
            if(!(modelFile.SelectedItem is string path))return;
            try
            {
                Cursor=Cursors.WaitCursor;models=BrawlLibrarySource.InspectModels(path);model.Items.Clear();
                foreach(var m in models)model.Items.Add(m.Name+"  ["+m.ResourceKey+"]");
                var primary=models.Select((m,i)=>new{m,i}).Where(p=>!p.m.Shadow).ToArray();
                if(primary.Length==1)model.SelectedIndex=primary[0].i;
                else identity.Text="Choose the exact source model; multiple candidates require review.";
                UpdateSelectedModel();
            }
            catch(Exception e){MessageBox.Show(this,e.Message,"Source model",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            finally{Cursor=Cursors.Default;}
        }
        private void UpdateSelectedModel()
        {
            if(model.SelectedIndex<0||model.SelectedIndex>=models.Count)return;
            var selected=models[model.SelectedIndex];
            identity.Text="Native MDL0 v"+selected.NativeVersion+" · "+selected.ResourceKey+" · skeleton "+selected.SkeletonSha256.Substring(0,12);
            // Give the source character its own searchable/collapsible label.
            // It starts from native evidence but may be renamed independently
            // of this exact model, costume or skeleton identity.
            character.Text=selected.Name;
        }
        private void Accept()
        {
            if(model.SelectedIndex<0||motionFile.SelectedIndex<0||string.IsNullOrWhiteSpace(character.Text)||string.IsNullOrWhiteSpace(version.Text))
            {MessageBox.Show(this,"Choose the original native model and motion package, character and game/build.");return;}
            Registration=new LibraryRegistration{ModelPath=(string)modelFile.SelectedItem,ModelKey=models[model.SelectedIndex].ResourceKey,
                MotionPath=(string)motionFile.SelectedItem,Character=character.Text.Trim(),Game=(string)game.SelectedItem,GameVersion=version.Text.Trim(),ApproveAssociation=approve.Checked};
            DialogResult=DialogResult.OK;Close();
        }
    }
}
