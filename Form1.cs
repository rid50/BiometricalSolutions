using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

using Neurotec.Biometrics;
using Neurotec.Images;
using Neurotec.DeviceManager;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Collections;
using System.Globalization;
using System.Threading;
using System.Resources;

using PSCBioVerification.Properties;

namespace PSCBioVerification
{
    public partial class Form1 : Form
    {
        private static readonly NRgb resultImageMinColor = new NRgb(0, 230, 0);
        private static readonly NRgb resultImageMaxColor = new NRgb(255, 255, 255);

        private BackgroundWorker backgroundWorkerProgressBar;
        private BackgroundWorker backgroundWorkerDataService;

        private bool enrollMode = true;
        private NFRecord template;
        private NFRecord enrolledTemplate;
        private FPScannerMan scannerMan;
        private string selectedScannerModules = string.Empty;
        private int userId = 0;

        //private WsqImage _wsqImage = null;
        private ArrayList _fingersCollection = null;
        private enum ProgramMode { Enroll = 1, Verify = 2, Identify = 3 };
        private ProgramMode mode;

        private const string appName = "Public Services Company (Kuwait) - ";

        public NFRecord Template
        {
            get
            {
                return template;
            }
        }

        public bool EnrollMode
        {
            get
            {
                return enrollMode;
            }
            set
            {
                enrollMode = value;
                //                OnEnrollModeChanged();
            }
        }

        public Form1()
        {
            setCulture();
            InitializeComponent();
        }

        System.Windows.Forms.ToolTip _toolTip;

        private void Form1_Load(object sender, EventArgs e)
        {
            backgroundWorkerProgressBar = new BackgroundWorker();
            backgroundWorkerProgressBar.WorkerSupportsCancellation = true;
            backgroundWorkerProgressBar.WorkerReportsProgress = true;
            backgroundWorkerProgressBar.DoWork += new DoWorkEventHandler(backgroundWorkerProgressBar_DoWork);
            backgroundWorkerProgressBar.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorkerProgressBar_RunWorkerCompleted);
            backgroundWorkerProgressBar.ProgressChanged += new ProgressChangedEventHandler(backgroundWorkerProgressBar_ProgressChanged);

            backgroundWorkerDataService = new BackgroundWorker();
            backgroundWorkerDataService.DoWork += new DoWorkEventHandler(backgroundWorkerDataService_DoWork);
            backgroundWorkerDataService.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorkerDataService_RunWorkerCompleted);

            Data.NFExtractor = new NFExtractor();
            Data.UpdateNfe();
            Data.UpdateNfeSettings();

            Data.NMatcher = new NMatcher();
            Data.UpdateNM();
            Data.UpdateNMSettings();

            ResourceManager rm = new ResourceManager("PSCBioVerification.Form1", this.GetType().Assembly);
            string selectedScannerModules = "Futronic";
            scannerMan = new FPScannerMan(selectedScannerModules, this);
            FPScanner scanner;
            if (scannerMan.Scanners.Count != 0)
            {
                scanner = scannerMan.Scanners[0];
                scanner.FingerPlaced += new EventHandler(scanner_FingerPlaced);
                scanner.FingerRemoved += new EventHandler(scanner_FingerRemoved);
                scanner.ImageScanned += new FPScannerImageScannedEventHandler(scanner_ImageScanned);
            }
            else
            {
                //ResourceManager rm = new ResourceManager("rmc", System.Reflection.Assembly.GetExecutingAssembly());
                string text = rm.GetString("msgNoScannersAttached"); // "No scanners attached"
                //string text = Resources.ResourceManager.GetString("msgNoScannersAttached"); // "No scanners attached"
                LogLine(text, true);
                ShowErrorMessage(text);
            }

            //buttonScan.Tag = true;

            _toolTip = new System.Windows.Forms.ToolTip();
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 100;
            _toolTip.OwnerDraw = true;
            _toolTip.ReshowDelay = 10;
            _toolTip.Draw += new DrawToolTipEventHandler(this.toolTip_Draw);
            _toolTip.Popup += new PopupEventHandler(toolTip_Popup);

            PictureBox pb; Label lb;
            for (int i = 0; i < 10; i++)
            {
                pb = this.Controls.Find("fpPictureBox" + (i + 1).ToString(), true)[0] as PictureBox;
                pb.MouseClick += new MouseEventHandler(fpPictureBox_MouseClick);

                if (pb.Tag != null)
                    _toolTip.SetToolTip(pb, rm.GetString(pb.Tag as string));

                lb = this.Controls.Find("lbFinger" + (i + 1).ToString(), true)[0] as Label;
                lb.Parent = pb;
                lb.Font = new Font("Areal", 10.0f, FontStyle.Bold);
                lb.BringToFront();
                lb.Location = new Point(0, pb.Height - 20);
            
            }

            ProgramMode mode = (ProgramMode)Settings.Default.ProgramMode;
            setMode(mode);
            setModeRadioButtons(mode);

            personId.Focus();
        }

        private void startCapturing()
        {
            FPScanner scanner;
            if (scannerMan.Scanners.Count != 0)
                scanner = scannerMan.Scanners[0];
            else
            {
                ResourceManager rm = new ResourceManager("PSCBioVerification.Form1", this.GetType().Assembly);
                string text = rm.GetString("msgNoScannersAttached"); // "No scanners attached"
                ShowErrorMessage(text);
                LogLine(text, true);
                return;
            }

            try
            {
                if (!scanner.IsCapturing)
                    scanner.StartCapturing();
            }
            catch (Exception ex)
            {
                string text = string.Format("Error starting capturing on scanner {0}: {1}", scanner.Id, ex.Message);
                ShowErrorMessage(text);
                //MessageBox.Show(text, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private void stopCapturing()
        {
            FPScanner scanner;
            if (scannerMan.Scanners.Count != 0)
                scanner = scannerMan.Scanners[0];
            else
                return;

            try
            {
                if (scanner.IsCapturing)
                    scanner.StopCapturing();
            }
            catch (Exception ex)
            {
                string text = string.Format("Error stoppping capturing on scanner {0}: {1}", scanner.Id, ex.Message);
                ShowErrorMessage(text);
                //MessageBox.Show(text, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        //Cursor _previousCursor;
        private void scanner_FingerPlaced(object sender, EventArgs e)
        {
            //startProgressBar();     // START
            //_previousCursor = Cursor.Current;
            //Cursor.Current = Cursors.WaitCursor;
        }

        private void scanner_FingerRemoved(object sender, EventArgs e)
        {
            //stopProgressBar();                     !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            //Cursor.Current = _previousCursor;
        }

        private void scanner_ImageScanned(object sender, FPScannerImageScannedEventArgs ea)
        {
            OnImage((NGrayscaleImage)ea.Image.Clone());
        }

        private bool enrollFromWSQ(WsqImage wsqImage)
        {
            if (!isUserIdValid())
                return false;

				MemoryStream ms = null;
            NImage nImage;
            try
            {
                ms = new MemoryStream(wsqImage.Content);

                nImage = NImageFormat.Wsq.LoadImage(ms);
            }
            catch (Exception ex)
            {
                string text = string.Format("Error creating image retrieved from database {0}", ex.Message);
                ShowErrorMessage(text);

                return false;
            }
            finally
            {
                if (ms != null)
                    ms.Dispose();
            }

            float horzResolution = nImage.HorzResolution;
            float vertResolution = nImage.VertResolution;
            if (horzResolution < 250) horzResolution = 500;
            if (vertResolution < 250) vertResolution = 500;

            NGrayscaleImage grayImage = (NGrayscaleImage)NImage.FromImage(NPixelFormat.Grayscale, 0, horzResolution, vertResolution, nImage);
            OnImage(grayImage);

            return true;
        }
        /*
                class MyNImageFormat : NImageFormat
                {
                    MyNImageFormat() {}


                }
        */
        private void clearView()
        {
            if (mode == ProgramMode.Enroll)
                nfView1.Image = null;
            nfView2.Image = null;
            nfView2.ResultImage = null;
            nfView2.Template = null;

            pictureBox1.Image = null;
            pictureBox2.Image = null;

        }

        private void clearFingerBoxes()
        {
            PictureBox pb;
            for (int i = 0; i <= 9; i++)
            {
                this.Controls.Find("lbFinger" + (i + 1).ToString(), true)[0].Text = "";
                pb = this.Controls.Find("fpPictureBox" + (i + 1).ToString(), true)[0] as PictureBox;
                //pb = this.Controls.Find("fpPictureBox" + (i + 1 < 9 ? (i + 1).ToString() : (i + 2).ToString()), true)[0] as PictureBox;
                pb.Image = null;
            }
        }

        private void clear()
        {
            ShowStatusMessage("");
            clearView();
            clearLog();

            this.template = null;
            this.enrolledTemplate = null;
            if (_fingersCollection != null)
                _fingersCollection.Clear();
            //LogWait();
        }

        private void OnImage(NGrayscaleImage image)
        {
            clearView();
            if (nfView1.Image == null)
                nfView1.Image = image.ToBitmap();

			NGrayscaleImage resultImage = (NGrayscaleImage)image.Clone();
            
			try
            {
                NfeExtractionStatus extractionStatus;
                template = Data.NFExtractor.Extract(resultImage, NFPosition.Unknown, NFImpressionType.LiveScanPlain, out extractionStatus);
                if (extractionStatus != NfeExtractionStatus.TemplateCreated)
                {
                    string text = string.Format("Extraction failed: {0}", extractionStatus.ToString());
                    ShowErrorMessage(text);

                    LogLine(text, true);
                    //LogLine("Waiting for image...", true);

                    pictureBox2.Image = Properties.Resources.redcross;

                    //      MessageBox.Show(text, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //    sw.Stop();
                    //stopProgressBar();
                    //UseWaitCursor = false;

                    return;
                }
            }
            catch (Exception e)
            {
                string text = string.Format("Extraction error: {0}", e.Message);
                ShowErrorMessage(text);

                LogLine(text, true);

                pictureBox2.Image = Properties.Resources.redcross;

                return;
            }
            finally
            {
                //WaitingForImageToScan();
                //stopProgressBar();                !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            }

            Bitmap bitmap;
            using (NImage ri = NImages.GetGrayscaleColorWrapper(resultImage, resultImageMinColor, resultImageMaxColor))
            {
                bitmap = ri.ToBitmap();
            }

            this.template = (NFRecord)template.Clone();
            nfView2.ResultImage = bitmap;
            if (nfView2.Template != null) nfView2.Template.Dispose();
            nfView2.Template = this.template;

            if (template == null)
            {
                ResourceManager rm = new ResourceManager("PSCBioVerification.Form1", this.GetType().Assembly);
                string text = rm.GetString("msgFingerprintImageIsOfLowQuality"); // "Fingerprint image is of low quality"
                ShowErrorMessage(text);
                LogLine(text, true);

                pictureBox2.Image = Properties.Resources.redcross;

                //MessageBox.Show(text, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            LogLine("Template extracted{0}. G: {1}. Size: {2}", true,
                Data.NFExtractor.UseQuality ? string.Format(". Quality: {0:P0}", Helpers.QualityToPercent(template.Quality) / 100.0) : null,
                template.G, Data.SizeToString(template.Save().Length));

            ShowStatusMessage(String.Format("Template extracted{0}. G: {1}. Size: {2}", true,
                Data.NFExtractor.UseQuality ? string.Format(". Quality: {0:P0}", Helpers.QualityToPercent(template.Quality) / 100.0) : null,
                template.G, Data.SizeToString(template.Save().Length)));

            switch (mode)
            {
                case ProgramMode.Enroll:
                    doEnroll();
                    nfView2.Zoom = 1F;
                    break;
                case ProgramMode.Verify:
                    doVerify();
                    nfView2.Zoom = 0.5F;
                    break;
            }

            WaitingForImageToScan();
        }

        private void WaitingForImageToScan()
        {
            ResourceManager rm2 = new ResourceManager("PSCBioVerification.Form1", this.GetType().Assembly);
            string text2 = rm2.GetString("msgWaitingForImage"); // "Waiting for image..."

            LogLine(text2, true);
            ShowStatusMessage(text2);
        }

        private void doEnroll()
        {
            this.enrolledTemplate = this.template;

            ProgramMode mode = ProgramMode.Verify;
            setMode(mode);
            setModeRadioButtons(mode);

            this.BeginInvoke(new MethodInvoker(delegate() { startCapturing(); }));

        }

        private void doVerify()
        {
            int score;

            NMMatchDetails matchDetails;
            try
            {
                score = Data.NMatcher.Verify(Template.Save(), this.enrolledTemplate.Save(), out matchDetails);
            }
            catch (Exception ex)
            {
                string text = string.Format("Error verifying templates: {0}", ex.Message);
                ShowErrorMessage(text);

                LogLine(string.Format("Error verifying templates: {0}", ex.Message), true);

                pictureBox2.Image = Properties.Resources.redcross;

                return;
            }

			string str = string.Format("Verification {0}", score == 0 ? "failed" : string.Format("succeeded. Score: {0}", score));

            LogLine(str, true);

            ShowStatusMessage(str);

            if (score > 0)
                pictureBox2.Image = Properties.Resources.checkmark;
            else
                pictureBox2.Image = Properties.Resources.redcross;

            if (score > 0)
            {
                startProgressBar();
                startDataServiceProcess();

            }
        }

        private void backgroundWorkerDataService_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            stopProgressBar();
            Application.DoEvents();

            if (e.Error != null)
            {
                LogLine(e.Error.Message, true); 
                ShowErrorMessage(e.Error.Message);
            }
            else
            {
                try
                {
                    if (mode == ProgramMode.Enroll)
                        processEnrolledData(e.Result as byte[]);
                    else if (mode == ProgramMode.Verify)
                    {
                        using (var ms = new MemoryStream(e.Result as byte[]))
                        {
                            pictureBox1.Image = Image.FromStream(ms);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogLine(ex.ToString(), true);
                    ShowErrorMessage(ex.ToString());
                }
            }
        }

        private delegate void LogHandler(string text, bool scroll, bool mainLog);

        private void Log(string text, bool scroll, bool mainLog)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new LogHandler(Log), new object[] { text, scroll, mainLog });
            }
            else
            {
                if (mainLog)
                {
                    rtbMain.AppendText(text);
                    if (scroll) rtbMain.ScrollToCaret();
                }
            }
        }

        private void clearLog()
        {
            rtbMain.Clear();
        }

        private void LogLine(bool mainLog)
        {
            Log(Environment.NewLine, true, mainLog);
        }

        private void Log(string text, bool mainLog)
        {
            Log(text, true, mainLog);
        }

        private void LogLine(string text, bool mainLog)
        {
            Log(text, false, mainLog);
            LogLine(mainLog);
        }

        private void Log(string format, bool mainLog, params object[] args)
        {
            Log(string.Format(format, args), mainLog);
        }

        private void LogLine(string format, bool mainLog, params object[] args)
        {
            LogLine(string.Format(format, args), mainLog);
        }

        private void setMode(ProgramMode mode)
        {
            switch (mode)
            {
                case ProgramMode.Enroll:
                    EnrollMode = true;
                    break;
                case ProgramMode.Identify:
                    EnrollMode = false;
                    break;
                case ProgramMode.Verify:
                    EnrollMode = false;
                    break;
            }

            this.mode = mode;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (scannerMan != null)
            {
                foreach (FPScanner scanner in scannerMan.Scanners)
                {
                    if (scanner.IsCapturing)
                        scanner.StopCapturing();
                }
                scannerMan.Dispose();
            }

            Data.NFExtractor.Dispose();
            Data.NMatcher.Dispose();
            //Data.Database.Dispose();

            Settings settings = Settings.Default;
            settings.NFRecordFromSize = WindowState != FormWindowState.Normal ? RestoreBounds.Size : Size;
            settings.NFRecordFormMaximized = WindowState == FormWindowState.Maximized;
            Settings.Default.Save();
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (Settings.Default.NFRecordFormMaximized) WindowState = FormWindowState.Maximized;
        }

        private void radioButtonGroup_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton)
            {
                RadioButton radiobutton = sender as RadioButton;
                switch (radiobutton.Text)
                {
                    case "Enroll":
                        mode = ProgramMode.Enroll;
                        break;
                    case "Verify":
                        mode = ProgramMode.Verify;
                        break;
                    case "Identify":
                        mode = ProgramMode.Identify;
                        break;
                }

                //setStatus(mode.ToString());
            }
        }

        private void setModeRadioButtons(ProgramMode mode)
        {
            switch (mode)
            {
                case ProgramMode.Enroll:
                    radioButtonEnroll.Enabled = true;
                    radioButtonEnroll.Checked = true;
                    radioButtonVerify.Enabled = false;
                    break;
                case ProgramMode.Verify:
                    radioButtonVerify.Enabled = true;
                    radioButtonVerify.Checked = true;
                    radioButtonEnroll.Enabled = false;
                    break;
                case ProgramMode.Identify:
                    radioButtonIdentify.Checked = true;
                    break;
            }
        }

        private void buttonRequest_Click(object sender, EventArgs e)
        {
            this.BeginInvoke(new MethodInvoker(delegate() { stopCapturing(); }));

            ProgramMode mode = ProgramMode.Enroll;
            setMode(mode);
            setModeRadioButtons(mode);

            clear();
            clearFingerBoxes();

            if (!isUserIdValid())
                return;

            startProgressBar();
            startDataServiceProcess();
        }

        private void processEnrolledData(byte[] serializedArrayOfWSQ)
        {
            PictureBox pb;

            ResourceManager rm = new ResourceManager("PSCBioVerification.Form1", this.GetType().Assembly);
            if (serializedArrayOfWSQ == null)
            {
                clearFingerBoxes();

                string text = rm.GetString("msgThePersonHasNotYetBeenEnrolled"); // "The person has not yet been enrolled"
                
                LogLine(text, true);
                ShowErrorMessage(text);
                return;
            }

            MemoryStream ms = new MemoryStream(serializedArrayOfWSQ);

            //Assembly.Load(string assemblyString)
            // Construct a BinaryFormatter and use it to deserialize the data to the stream.
            BinaryFormatter formatter = new BinaryFormatter();
            try
            {
                formatter.Binder = new GenericBinder<WsqImage>();
                _fingersCollection = formatter.Deserialize(ms) as ArrayList;
            }
            catch (SerializationException ex)
            {
                LogLine(ex.Message, true);
                ShowErrorMessage(ex.Message);
                return;
            }
            finally
            {
                ms.Close();
            }

            int bestQuality = 0;
            int bestQualityRadioButton = 0;
            RadioButton rb = null; Label lab = null; WsqImage wsqImage = null;

            for (int i = 0; i < _fingersCollection.Count; i++)
            {
                Control[] control = this.Controls.Find("radioButton" + (i + 1).ToString(), true);
                Control[] controlLab = this.Controls.Find("label" + (i + 1).ToString(), true);
                if (control.Length == 0)
                    continue;

                rb = control[0] as RadioButton;
                lab = controlLab[0] as Label;

                wsqImage = _fingersCollection[i] as WsqImage;
                if (wsqImage == null)
                {
                    rb.Enabled = false;
                    lab.Enabled = false;
                }
                else
                {
                    rb.Enabled = true;
                    lab.Enabled = true;
                }

                pb = this.Controls.Find("fpPictureBox" + (i + 1).ToString(), true)[0] as PictureBox;
                
                if (_fingersCollection[i] != null)
                {
                    try
                    {
                        ms = new MemoryStream(wsqImage.Content);

                        NImage nImage = NImageFormat.Wsq.LoadImage(ms);

                        float horzResolution = nImage.HorzResolution;
                        float vertResolution = nImage.VertResolution;
                        if (horzResolution < 250) horzResolution = 500;
                        if (vertResolution < 250) vertResolution = 500;

                        NGrayscaleImage grayImage = (NGrayscaleImage)NImage.FromImage(NPixelFormat.Grayscale, 0, horzResolution, vertResolution, nImage);
                        int q = GetImageQuality(grayImage, this.Controls.Find("lbFinger" + (i + 1).ToString(), true)[0] as Label);

                        if (bestQuality < q)
                        {
                            bestQuality = q;
                            bestQualityRadioButton = i;
                        }

                        pb.Image = nImage.ToBitmap();
                        pb.SizeMode = PictureBoxSizeMode.Zoom;

                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    finally
                    {
                        ms.Close();
                    }

                }
                else
                {
                    pb.Image = null;
                    this.Controls.Find("lbFinger" + (i + 1).ToString(), true)[0].Text = "";
                }
            }

            stopProgressBar();

            rb = this.Controls.Find("radioButton" + (bestQualityRadioButton + 1).ToString(), true)[0] as RadioButton;
            this.BeginInvoke(new MethodInvoker(delegate() { checkRadioButton(rb.Name); }));
        }

        private int GetImageQuality(NGrayscaleImage image, Label lb)
        {
            NGrayscaleImage resultImage = (NGrayscaleImage)image.Clone();
            try
            {
                NfeExtractionStatus extractionStatus;
                template = Data.NFExtractor.Extract(resultImage, NFPosition.Unknown, NFImpressionType.LiveScanPlain, out extractionStatus);
                if (extractionStatus != NfeExtractionStatus.TemplateCreated)
                {
                    lb.Text = string.Format("Q: {0:P0}", 0);
                    lb.ForeColor = Color.Red;
                    return 0;
                }
            }
            catch (Exception)
            {
                lb.Text = string.Format("Q: {0:P0}", 0);
                lb.ForeColor = Color.Red;
                return 0;
            }

            this.template = (NFRecord)template.Clone();
            int i = 0;
            if (template != null)
            {
                i = Helpers.QualityToPercent(template.Quality);
                lb.Text = string.Format("Q: {0:P0}", i / 100.0);
                if (i > 80)
                    lb.ForeColor = Color.GreenYellow;
                else if (i > 50)
                    lb.ForeColor = Color.Orange;
                else
                    lb.ForeColor = Color.Red;
            } 
            else
            {
                lb.Text = string.Format("Q: {0:P0}", 0);
                lb.ForeColor = Color.Red;
            }

            return i;
        }

        // Determines the correct size for the button2 ToolTip.
        private void toolTip_Popup(object sender, PopupEventArgs e)
        {
            if (e.AssociatedControl == fpPictureBox2)
            {
                using (Font f = new Font("Tahoma", 9))
                {
                    e.ToolTipSize = TextRenderer.MeasureText(_toolTip.GetToolTip(e.AssociatedControl), f);
                }
            }
        }

        // Handles drawing the ToolTip.
        private void toolTip_Draw(System.Object sender,
            System.Windows.Forms.DrawToolTipEventArgs e)
        {

            // Draw the ToolTip differently depending on which 
            // control this ToolTip is for.
            // Draw a custom 3D border if the ToolTip is for button1.
            //if (e.AssociatedControl == fpPictureBox1)
            {
                // Draw the standard background.
                e.DrawBackground();

                // Draw the custom border to appear 3-dimensional.
                e.Graphics.DrawLines(SystemPens.ControlLightLight, new Point[] {
                    new Point (0, e.Bounds.Height - 1), 
                    new Point (0, 0), 
                    new Point (e.Bounds.Width - 1, 0)
                });
                e.Graphics.DrawLines(SystemPens.ControlDarkDark, new Point[] {
                    new Point (0, e.Bounds.Height - 1), 
                    new Point (e.Bounds.Width - 1, e.Bounds.Height - 1), 
                    new Point (e.Bounds.Width - 1, 0)
                });

                // Specify custom text formatting flags.
                TextFormatFlags sf = TextFormatFlags.VerticalCenter |
                                     TextFormatFlags.HorizontalCenter |
                                     TextFormatFlags.NoFullWidthCharacterBreak;

                // Draw the standard text with customized formatting options.
                e.DrawText(sf);
            }
        }

        private void checkRadioButton(string rbName)
        {
            RadioButton rb = this.Controls.Find(rbName, true)[0] as RadioButton;
            //rb.Checked = true;
            this.InvokeOnClick(rb, new EventArgs());
        }

        private bool isUserIdValid()
        {
            switch (mode)
            {
                case ProgramMode.Enroll:
                case ProgramMode.Verify:
                    if (!Int32.TryParse(personId.Text, out this.userId))
                    {
                        ResourceManager rm = new ResourceManager("PSCBioVerification.Form1", this.GetType().Assembly);
                        string text = rm.GetString("msgEnterPersonId"); // "Enter Person Id"
                        ShowErrorMessage(text);
                        LogLine(text, true);
                        return false;
                    }
                    break;
            }

            return true;
        }

        private void fingerChanged(int fingerNumber)
        {
            MyPictureBox pb;
            for (int i = 0; i < _fingersCollection.Count; i++)
            {
                pb = this.Controls.Find("fpPictureBox" + (i + 1).ToString(), true)[0] as MyPictureBox;

                if (i == fingerNumber)
                {
                    pb.Active = true;
                    pb.Invalidate();
                } 
                else
                {
                    if (pb.Active)
                    {
                        pb.Active = false;
                        pb.Invalidate();
                    }
                }
            }
        }

        private void fpPictureBox_MouseClick(object sender, EventArgs e)
        {
            PictureBox rb = sender as PictureBox;
            if (rb.Image == null)
                return;

            int rbNumber = "fpPictureBox".Length;
            this.BeginInvoke(new MethodInvoker(delegate() { checkRadioButton("radioButton" + rb.Name.Substring(rbNumber)); }));
        }

        private void radioButton1_Click(object sender, EventArgs e)
        {
            if (_fingersCollection == null || _fingersCollection.Count == 0)
                return;

            this.BeginInvoke(new MethodInvoker(delegate() { stopCapturing(); }));

            ProgramMode mode = ProgramMode.Enroll;
            setMode(mode);
            setModeRadioButtons(mode);

            clearLog();

            RadioButton rb = sender as RadioButton;
            int rbNumber = "radioButton".Length;
            rbNumber = Int32.Parse(rb.Name.Substring(rbNumber));
            WsqImage wsqImage = _fingersCollection[rbNumber - 1] as WsqImage;
            fingerChanged(rbNumber - 1);

            enrollFromWSQ(wsqImage);
        }

        void ShowStatusMessage(string message)
        {
            toolStripStatusLabelError.ForeColor = Color.Black;
            toolStripStatusLabelError.Text = message;
            Application.DoEvents();
        }

        void ShowErrorMessage(string message)
        {
            stopProgressBar();
            Application.DoEvents();

            toolStripStatusLabelError.ForeColor = Color.Red;
            toolStripStatusLabelError.Text = message;
        }

        private void setCulture()
        {
            String culture = null;

            if (System.Configuration.ConfigurationManager.AppSettings["Culture"] != null)
                culture = System.Configuration.ConfigurationManager.AppSettings["Culture"].ToString();

            if (culture != null)
            {
                setCulture(culture);
            }
        }

        private void setCulture(string culture)
        {
            if (culture != null)
            {
                try
                {
                    System.Globalization.CultureInfo info = new System.Globalization.CultureInfo(culture);
                    Thread.CurrentThread.CurrentCulture = info;
                    Thread.CurrentThread.CurrentUICulture = info;
                }
                catch (ArgumentException) { }
            }
        }

        private void changeCulture(string culture)
        {
            setCulture(culture);

            foreach (Control c in this.Controls)
            {
                ComponentResourceManager resources = new ComponentResourceManager(this.GetType());
                resources.ApplyResources(c, c.Name, new CultureInfo(culture));
            }
        }
    }

    [Serializable]
    public class WsqImage
    {
        public int XSize { get; set; }
        public int YSize { get; set; }
        public int XRes { get; set; }
        public int YRes { get; set; }
        public int PixelFormat { get; set; }
        public byte[] Content { get; set; }
    }

    public class GenericBinder<T> : System.Runtime.Serialization.SerializationBinder
    {
        /// <summary>
        /// Resolve type
        /// </summary>
        /// <param name="assemblyName">eg. App_Code.y4xkvcpq, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null</param>
        /// <param name="typeName">eg. String</param>
        /// <returns>Type for the deserializer to use</returns>
        public override Type BindToType(string assemblyName, string typeName)
        {
            // We're going to ignore the assembly name, and assume it's in the same assembly 
            // that <T> is defined (it's either T or a field/return type within T anyway)

            string[] typeInfo = typeName.Split('.');
            bool isSystem = (typeInfo[0].ToString() == "System");
            string className = typeInfo[typeInfo.Length - 1];

            // noop is the default, returns what was passed in
            //Type toReturn = Type.GetType(string.Format("{0}, {1}", typeName, assemblyName));
            Type toReturn = null;
            try
            {
                toReturn = Type.GetType(string.Format("{0}, {1}", typeName, assemblyName));
            }
            catch (System.IO.FileLoadException) { }

            if (!isSystem && (toReturn == null))
            {   // don't bother if system, or if the GetType worked already (must be OK, surely?)
                System.Reflection.Assembly a = System.Reflection.Assembly.GetAssembly(typeof(T));
                string assembly = a.FullName.Split(',')[0];   //FullName example: "App_Code.y4xkvcpq, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
                if (a == null)
                {
                    throw new ArgumentException("Assembly for type '" + typeof(T).Name.ToString() + "' could not be loaded.");
                }
                else
                {
                    Type newtype = a.GetType(assembly + "." + className);
                    if (newtype == null)
                    {
                        throw new ArgumentException("Type '" + typeName + "' could not be loaded from assembly '" + assembly + "'.");
                    }
                    else
                    {
                        toReturn = newtype;
                    }
                }
            }
            return toReturn;
        }
    }
}
