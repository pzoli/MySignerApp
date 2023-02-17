using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WebSocketSharp.Server;
using WebSocketSharp.Net;
using Microsoft.Win32;
using Security.Cryptography.X509Certificates;

namespace MySignerApp
{
    public partial class MainForm : Form
    {
        private StoreName store = StoreName.My;
        private StoreLocation location = StoreLocation.CurrentUser;
        private X509Store x509Store;
        private WebSocketServer wssrv;
        public X509Certificate2 certSelected;

        static public List<SignerForm> signerFromList = new List<SignerForm>();
        public bool isClosedByContextMenu = false;
        private String defaultCertName;
        private const string keyName = @"HKEY_CURRENT_USER\Software\SysdataPSE";

        public MainForm()
        {
            InitializeComponent();
            onCreate();
        }
        private void onCreate()
        {
            MyBehavior.mainForm = this;
            wssrv = new WebSocketServer("ws://localhost:8553");
            wssrv.AuthenticationSchemes = AuthenticationSchemes.Basic;
            wssrv.Realm = "Sysdata-PSE WebSocket";
            wssrv.UserCredentialsFinder = id => {
                var name = id.Name;

                // Return user name, password, and roles.
                return name == "sysdatapse"
                       ? new NetworkCredential(name, "sysdatapwd", "localhost")
                       : null; // If the user credentials are not found.
            };
            wssrv.AddWebSocketService<MyBehavior>("/signer");
            wssrv.Start();
            hideWindow();
            setDefaultCertFromRegistry();
        }
        internal void removeSignerForm(SignerForm signerForm)
        {
            signerFromList.Remove(signerForm);
        }

        private void setDefaultCertFromRegistry()
        {
            if (defaultCertName == null)
            {
                string valueName = "defaultCertName";
                defaultCertName = (String)Registry.GetValue(keyName, valueName, null);
                if (defaultCertName != null)
                {
                    x509Store = new X509Store(store, location);
                    x509Store.Open(OpenFlags.ReadOnly);
                    X509Certificate2Collection col = getCertificates(x509Store);
                    foreach (X509Certificate2 cert in col)
                    {
                        if (cert.Subject.Equals(defaultCertName))
                        {
                            setCertSelected(cert);
                            break;
                        }
                    }
                    x509Store.Close();
                }
            }

        }

        internal SignerForm createSignerForm(MyBehavior myBehavior)
        {
            SignerForm signerForm = new SignerForm(myBehavior);
            signerFromList.Add(signerForm);
            return signerForm;
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            showWindow();
        }

        private void hideWindow()
        {
            WindowState = FormWindowState.Minimized;
            notifyIcon.Visible = true;
            Hide();
        }

        public void showWindow()
        {
            TopMost = true;
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
            Focus();

            TopMost = false;

            notifyIcon.Visible = false;
        }

        private void SignerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isClosedByContextMenu)
            {
                wssrv.Stop();
            }
            else
            {
                e.Cancel = true;
                hideWindow();
            }
        }

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showWindow();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            isClosedByContextMenu = true;
            Close();
        }

        private void btnChooseCert_Click(object sender, EventArgs e)
        {
            string windowTitle = "Select certificate";
            string windowMsg = "Please select your active certificate";

            x509Store = new X509Store(store, location);
            x509Store.Open(OpenFlags.ReadOnly);


            X509Certificate2Collection col = getCertificates(x509Store);

            if (col.Count == 1)
            {
                certSelected = col[0];
                setCertSelected(certSelected);
            }
            else if (col.Count == 0)
            {
                MessageBox.Show("You have no valid certificates!");
            }
            else
            {
                X509Certificate2Collection sel = X509Certificate2UI.SelectFromCollection(col, windowTitle, windowMsg, X509SelectionFlag.SingleSelection);
                certSelected = null;
                if (sel.Count > 0)
                {
                    X509Certificate2Enumerator en = sel.GetEnumerator();
                    en.MoveNext();
                    certSelected = en.Current;
                    setCertSelected(certSelected);
                }
                else
                {
                    statCert.Text = "Selected certificate: ";
                }
            }
            x509Store.Close();

        }

        private X509Certificate2Collection getCertificates(X509Store x509Store)
        {
            X509Certificate2Collection col = x509Store.Certificates;
            var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

            chain.ChainPolicy.VerificationTime = DateTime.Now;
            for (int i = col.Count - 1; i > -1; i--)
            {
                string issuerName = col[i].IssuerName.Name.ToLower();
                bool elementValid = chain.Build(col[i]);
                bool hasCngKey = X509CertificateExtens‌​ionMethods.HasCngKey(col[i]);
                if (!(elementValid && (issuerName.Contains("dc=sysdata-pse") || issuerName.Contains("o=sysdata-pse")) && (col[i].HasPrivateKey || hasCngKey)))
                {
                    col.RemoveAt(i);
                }
            }

            return col;
        }

        public void setCertSelected(X509Certificate2 cert)
        {
            certSelected = cert;
            string valueName = "defaultCertName";
            Registry.SetValue(keyName, valueName, cert.Subject);
            statCert.Text = "Selected certificate: " + cert.Subject.Substring(0,20);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                hideWindow();
            }
        }
    }
}
