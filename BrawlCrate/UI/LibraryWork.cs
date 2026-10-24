using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal static class LibraryWork
    {
        internal static T Run<T>(IWin32Window owner,string title,Func<CancellationToken,IProgress<string>,T> work) where T:class
        {
            T result=null;Exception error=null;bool complete=false;
            using(var cancel=new CancellationTokenSource())
            using(var dialog=new Form{Text=title,ClientSize=new Size(560,118),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false})
            {
                var label=new Label{Text="Reading source metadata...",Bounds=new Rectangle(14,12,532,34)};
                var progress=new ProgressBar{Style=ProgressBarStyle.Marquee,Bounds=new Rectangle(14,50,532,18)};
                var button=new Button{Text="Cancel",Bounds=new Rectangle(456,80,90,27)};button.Click+=(s,e)=>{cancel.Cancel();button.Enabled=false;label.Text="Cancelling; original sources remain unchanged...";};
                dialog.Controls.AddRange(new Control[]{label,progress,button});
                dialog.FormClosing+=(s,e)=>{if(!complete){e.Cancel=true;cancel.Cancel();}};
                var report=new Progress<string>(message=>{if(!dialog.IsDisposed&&!cancel.IsCancellationRequested)label.Text=message;});
                dialog.Shown+=(s,e)=>Task.Run(()=>work(cancel.Token,report),cancel.Token).ContinueWith(task=>{
                    complete=true;if(task.IsFaulted)error=task.Exception.GetBaseException();else if(!task.IsCanceled)result=task.Result;dialog.Close();
                },TaskScheduler.FromCurrentSynchronizationContext());
                dialog.ShowDialog(owner);
            }
            if(error!=null&&!(error is OperationCanceledException))throw error;
            return result;
        }
    }
}
