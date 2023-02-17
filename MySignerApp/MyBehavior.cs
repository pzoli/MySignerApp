using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Text.Json;

namespace MySignerApp
{
    public class MyBehavior : WebSocketBehavior
    {
        public SignerForm signerForm;
        public static MainForm mainForm;

        public MyBehavior()
        {
            mainForm.Invoke((MethodInvoker)delegate ()
            {
                if (mainForm != null)
                {
                    signerForm = mainForm.createSignerForm(this);
                }
            });
        }
        protected override void OnMessage(MessageEventArgs e)
        {
            ActionMessage msg = JsonSerializer.Deserialize<ActionMessage>(e.Data);
            if (string.IsNullOrEmpty(msg.action))
                return;
            if (signerForm == null)
                return;
            if (msg.action.Equals("show"))
            {
                mainForm.Invoke((MethodInvoker)delegate ()
                {
                    signerForm.webBrowser.Navigate("about:blank");
                    signerForm.webBrowser.Document.OpenNew(true);
                    signerForm.webBrowser.Document.Write(msg.paramsJSON);
                    signerForm.webBrowser.Refresh();
                    if (signerForm.certSelected == null)
                    {
                        signerForm.setCertSelected(mainForm.certSelected);
                    }
                    signerForm.updateCertContent();
                    signerForm.showWindow();
                });
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                ActionMessage resultMsg = new ActionMessage();
                resultMsg.action = "result";
                resultMsg.paramsJSON = "visible";
                string jsonString = JsonSerializer.Serialize(resultMsg, options);
                Send(jsonString) ;
            }
        }

        protected override void OnOpen()
        {

        }

        protected override void OnClose(CloseEventArgs e)
        {
            if (signerForm != null)
            {
                mainForm.Invoke((MethodInvoker)delegate ()
                {
                    mainForm.removeSignerForm(signerForm);
                    signerForm.isClosedByContextMenu = true;
                    signerForm.Close();
                });
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {

        }

        public void SendMessage(String msg)
        {
            Send(msg);
        }
    }
}
