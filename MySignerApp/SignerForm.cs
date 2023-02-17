using Security.Cryptography.X509Certificates;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace MySignerApp
{
    public partial class SignerForm : Form
    {

        private MyBehavior myBehavior;

        private StoreName store = StoreName.My;
        private StoreLocation location = StoreLocation.CurrentUser;
        private X509Store x509Store;

        public X509Certificate2 certSelected { get; set; }
        private String defaultCertName;
        public String b64Signature { get; set; }

        private byte[] signature;
        private byte[] md5Hexdata;
               
        public bool isClosedByContextMenu = false;

        public SignerForm(MyBehavior myBehavior)
        {
            InitializeComponent();
            this.myBehavior = myBehavior;
            hideWindow();
        }

        public void btnChooseCert_Click(object sender, EventArgs e)
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
                    defaultCertName = certSelected.Subject;

                    setCertSelected(certSelected);
                }
                else
                {
                    statCert.Text = "Selected certificate: ";
                }
            }
            x509Store.Close();

        }
        public void updateCertContent()
        {
            if (certSelected != null)
            {
                HtmlElement certElement = webBrowser.Document.GetElementById("cert");
                if (certElement != null)
                {
                    certElement.InnerText = certSelected.Subject;

                    string content = webBrowser.Document.GetElementsByTagName("HTML")[0].OuterHtml;
                    webBrowser.Navigate("about:blank");
                    webBrowser.Document.OpenNew(true);

                    webBrowser.Document.Write(content);
                    webBrowser.Refresh();
                }
            }

        }
        public void setCertSelected(X509Certificate2 cert)
        {
            certSelected = cert;
            if (certSelected != null)
            {
                statCert.Text = "Selected certificate: " + certSelected.Subject.Substring(0,20);
                updateCertContent();
            }
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
                if (!(elementValid && (col[i].HasPrivateKey || hasCngKey)))
                {
                    col.RemoveAt(i);
                }
            }
            
            return col;
        }

        public void btnSign_Click(object sender, EventArgs e)
        {
            if (certSelected == null)
            {
                MessageBox.Show("You have no certification selected to sign!");
                return;
            }

            String trasformedValue = webBrowser.DocumentText;
            if (string.IsNullOrEmpty(trasformedValue))
            {
                MessageBox.Show("Signed data is null!");
                return;
            }

            x509Store = new X509Store(store, location);
            x509Store.Open(OpenFlags.ReadOnly);
            md5Hexdata = getHexDataFromDescriptor(trasformedValue);

            if (X509CertificateExtens‌​ionMethods.HasCngKey(certSelected))
            {
                CngKey Key = certSelected.GetCngPrivateKey();
                Security.Cryptography.RSACng alg = new Security.Cryptography.RSACng(Key);
                alg.SignatureHashAlgorithm = CngAlgorithm.Sha1;
                signature = alg.SignData(md5Hexdata);
            }
            else if (certSelected.HasPrivateKey)
            {
                RSACryptoServiceProvider RSAalg = (RSACryptoServiceProvider)certSelected.PrivateKey;
                RSAParameters Key = RSAalg.ExportParameters(true);
                signature = HashAndSignBytes(md5Hexdata, Key);
            } else
            {
                signature = null;
            }
            if (signature != null) { 
                b64Signature = Convert.ToBase64String(signature);
                tslStatus.Text = "Status: signed.";
                btnVerify.Enabled = true;

                if (myBehavior != null)
                {
                    string pemFormat = certSelected.GetRSAPublicKey().ToXmlString(false);
                    string textDoc = webBrowser.DocumentText;
                    ResponseMessage response = new ResponseMessage();
                    response.action = "signature";
                    response.textDoc = textDoc;
                    response.textSign = b64Signature;
                    response.pubkey = pemFormat;
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string jsonString = JsonSerializer.Serialize(response, options);
                    myBehavior.SendMessage(jsonString);
                }
                hideWindow();
            }
            else
            {
                MessageBox.Show("Signed data is null!");
            }
            
            x509Store.Close();            
        }

        public byte[] getHexDataFromDescriptor(String input)
        {
            UTF8Encoding encoding = new UTF8Encoding(true);
            byte[] data = encoding.GetBytes(input);
            byte[] hash = MD5.Create().ComputeHash(data);
            string hex = BitConverter.ToString(hash).ToLower();
            hex = hex.Replace("-", "");
            return Encoding.ASCII.GetBytes(hex);
        }

        public static byte[] HashAndSignBytes(byte[] DataToSign, RSAParameters Key)
        {
            try
            {
                RSACryptoServiceProvider RSAalg = new RSACryptoServiceProvider();
                RSAalg.ImportParameters(Key);
                return RSAalg.SignData(DataToSign, new SHA1CryptoServiceProvider());
            }
            catch (CryptographicException e)
            {
                return null;
            }
        }

        private void btnVerify_Click(object sender, EventArgs e)
        {
            RSACryptoServiceProvider RSA = new RSACryptoServiceProvider();
            String myRSAParameters = certSelected.PublicKey.Key.ToXmlString(false);
            MessageBox.Show("Sign valid: " +VerifySignedHash(md5Hexdata,signature, myRSAParameters));
        }

        public static bool VerifySignedHash(byte[] DataToVerify, byte[] signature, String publicKey)
        {
            try
            {
                RSACryptoServiceProvider RSAalg = new RSACryptoServiceProvider();
                RSAalg.FromXmlString(publicKey);
                return RSAalg.VerifyData(DataToVerify, new SHA1CryptoServiceProvider(), signature);
            }
            catch (CryptographicException e)
            {
                return false;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isClosedByContextMenu) { 
                e.Cancel = true;
                hideWindow();
            }
        }

        private void rtbMessage_TextChanged(object sender, EventArgs e)
        {
            tslStatus.Text = "Status: changed";
            btnVerify.Enabled = false;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            myBehavior.signerForm = null;
            isClosedByContextMenu = true;
            Close();
        }

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showWindow();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                hideWindow();
            }
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

    }
}
