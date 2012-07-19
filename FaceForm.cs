using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Threading;
using System.Resources;

using Neurotec.Biometrics;
using Neurotec.Images;
using Neurotec.Devices;

using Neurotec.Media;
using Neurotec.Biometrics.Gui;
using Neurotec.IO;

using PSCBioVerificationFace.Common;
using DBHelper;

namespace PSCBioVerificationFace
{
    public partial class FaceForm : Form
    {
        private static readonly NRgb resultImageMinColor = new NRgb(0, 230, 0);
        private static readonly NRgb resultImageMaxColor = new NRgb(255, 255, 255);

        private static string _lastSelectedCamera = string.Empty;

        private readonly NLExtractor _extractor = null;
        private readonly NDeviceManager _deviceManager = null;
        private object _readerLock = new object();
        private NCamera _camera = null;
        private NMediaReader _videoReader = null;
        private FaceRecord _newRecord = null;

        private List<byte[]> _enrolledTemplateList = null;
        private List<byte[]> _capturedTemplateList = null;
        
        private NVideoFormat _pendingFormat = null;
        private bool _fromCamera = true;
        private bool _pause = false;

        private BackgroundWorker backgroundWorker;

        private bool enrollMode = true;
        private int userId = 0;

        private ArrayList _fingersCollection = null;
        private enum ProgramMode { Enroll = 1, Verify = 2, Identify = 3 };
        private ProgramMode _mode;

        private const string appName = "Public Services Company (Kuwait) - ";

        public bool EnrollMode
        {
            get
            {
                return enrollMode;
            }
            set
            {
                enrollMode = value;
            }
        }

        public FaceForm()
        {
            setCulture();
            InitializeComponent();

            _extractor = Tools.Extractor;
            _deviceManager = Devices.Instance.Cameras;
            chbLiveView.Checked = false;

            _deviceManager.DeviceAdded += new EventHandler<NDeviceManagerDeviceEventArgs>(devMan_DeviceAdded);
            _deviceManager.DeviceRemoved += new EventHandler<NDeviceManagerDeviceEventArgs>(devMan_DeviceRemoved);
            ListAllDevices();

            backgroundWorker = new System.ComponentModel.BackgroundWorker();
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += new DoWorkEventHandler(backgroundWorker_DoWork);
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);
        }

        System.Windows.Forms.ToolTip _toolTip;

        private void FaceForm_Load(object sender, EventArgs e)
        {

            Tools.LoadExtractorSettings();

            ResourceManager rm = new ResourceManager("PSCBioVerificationFace.Form1", this.GetType().Assembly);
            //buttonScan.Tag = true;

            _toolTip = new System.Windows.Forms.ToolTip();
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 100;
            _toolTip.OwnerDraw = true;
            _toolTip.ReshowDelay = 10;
            _toolTip.Draw += new DrawToolTipEventHandler(this.toolTip_Draw);

            setMode(_mode);
            setModeRadioButtons(_mode);

            personId.Focus();
        }

        private void enrollFromImage(bool toView2)
        {
            NImage nImage = null;

            if (System.Configuration.ConfigurationManager.AppSettings["Enroll"] != "file")
            {
                byte[] buffer = null;
                DBHelper.DBUtil db = new DBHelper.DBUtil();
                if (System.Configuration.ConfigurationManager.AppSettings["Enroll"] == "service")
                    buffer = db.GetImageFromWebService(IMAGE_TYPE.picture, this.userId);
                else
                    buffer = db.GetImage(IMAGE_TYPE.picture, this.userId);

                MemoryStream ms = null;
                try
                {
                    if (buffer != null)
                        ms = new MemoryStream(buffer);

                    if (ms != null)
                        nImage = NImage.FromStream(ms);
                }
                catch (Exception ex)
                {
                    ShowError(string.Format("Error creating image retrieved from database {0}", ex.Message));
                    return;
                }
                finally
                {
                    if (ms != null)
                        ms.Dispose();
                }
            }
            else
            {
                if (!toView2)
                    nImage = NImage.FromFile(Path.GetDirectoryName(Application.ExecutablePath) + "\\Roman.jpg");
                else
                    nImage = NImage.FromFile(Path.GetDirectoryName(Application.ExecutablePath) + "\\Roman2.jpg");
            }

            try
            {
                if (!toView2)
                    nlView1.Image = nImage.ToBitmap();
                else
                    nlView2.Image = nImage.ToBitmap();

                using (NGrayscaleImage gray = nImage.ToGrayscale())
                {
                    NleDetectionDetails details;
                    NleExtractionStatus status;
                    try { _extractor.DetectAllFeaturePoints = Settings.Default.ExtractorDetectAllFeaturesNonLive; }
                    catch { }
                    NLTemplate template = _extractor.Extract(gray, out details, out status);
                    if (status != NleExtractionStatus.TemplateCreated)
                    {
                        _newRecord = null;
//                        ShowError(string.Format("Template extraction failed: {0}", status));
                        String descr = getStatusDescription(status);
                        if (descr != String.Empty)
                            ShowError(string.Format("Template extraction failed: {0}", descr));
                        else
                            ShowError(string.Format("Template extraction failed: {0}", status));
                        return;
                    }
                    else
                    {
                        _newRecord = new FaceRecord(template, nImage, details);
                        if (!toView2)
                        {
                            _enrolledTemplateList = new List<byte[]>();
                            _enrolledTemplateList.Add(template.Save().ToByteArray());
                        }
                        else
                        {
                            _capturedTemplateList = new List<byte[]>();
                            _capturedTemplateList.Add(template.Save().ToByteArray());
                        }
                        template.Dispose();
                    }

                    if (!toView2)
                        SetImageToView(nlView1, nImage.ToBitmap(), new NleDetectionDetails[] { details }, status, 100, TimeSpan.Zero);
                    else
                        SetImageToView(nlView2, nImage.ToBitmap(), new NleDetectionDetails[] { details }, status, 100, TimeSpan.Zero);
                }

            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            return;
        }

        private delegate void SetImageToViewDelegate(NLView nlView, Bitmap image, NleDetectionDetails[] details, NleExtractionStatus status, int extractionPercentDone, TimeSpan timeStamp);
        private void SetImageToView(NLView nlView, Bitmap image, NleDetectionDetails[] details, NleExtractionStatus status, int extractionPercentDone, TimeSpan timeStamp)
        {

            if (InvokeRequired)
            {
                if (_mode != ProgramMode.Enroll)
                    BeginInvoke(new SetImageToViewDelegate(SetImageToView), nlView, image, details, status, extractionPercentDone, timeStamp);

                return;
            }

            Bitmap bmp = nlView.Image;
            nlView.Image = image;
            if (bmp != null && bmp != image) bmp.Dispose();

            nlView.DetectionDetails = details;

            if (extractionPercentDone > 0 && extractionPercentDone < 100)
            {
                toolStripProgressBar.Value = extractionPercentDone;
                //toolStripProgressBar.Visible = true;
            }
            else
            {
                toolStripProgressBar.Value = 0;
                //pbExtractionProgress.Visible = false;
            }

            if (_mode == ProgramMode.Enroll)
            {
                if (_newRecord == null)
                {
                    int count = 0;
                    if (details != null) count = details.Length;
                    LogLine(string.Format("Live view: {0} face(s) detected.", count), true);
                }
                else
                {
                    if (details != null)
                        LogLine(string.Format("Template created. Live view: {0} face(s) detected.", details.Length), true);
                }
            }

            String descr = getStatusDescription(status);
            if (descr != String.Empty)
                ShowError(descr);
        }

        private String getStatusDescription(NleExtractionStatus status) {
            switch (status)
            {
                case NleExtractionStatus.EyesNotDetected:
                    return "Eyes not detected";
                case NleExtractionStatus.FaceNotDetected:
                    return "Face not detected";
                case NleExtractionStatus.FaceTooCloseToImageBorder:
                    return "Face too close to image border";
                case NleExtractionStatus.GeneralizationFailed:
                    return "Generalization failed";
                case NleExtractionStatus.LivenessCheckFailed:
                    return "Liveness check failed";
                case NleExtractionStatus.QualityCheckExposureFailed:
                    return "Quality check failed";
                case NleExtractionStatus.QualityCheckGrayscaleDensityFailed:
                    return "Quality check: grayscale density failed";
                case NleExtractionStatus.QualityCheckSharpnessFailed:
                    return "Quality check: sharpness failed";
                case NleExtractionStatus.TemplateCreated:
                    //    ShowError("Template created");
                    return String.Empty;
                case NleExtractionStatus.None:
                    //ShowError("");
                    return String.Empty;
                default:
                    return status.ToString();
            }
        }

        private void zoomSlider1_ZoomValueChanged(object sender, ZoomEventArgs e)
        {
            nlView1.Zoom = e.ZoomValue;
        }

        private void zoomSlider2_ZoomValueChanged(object sender, ZoomEventArgs e)
        {
            nlView2.Zoom = e.ZoomValue;
        }


        private void faceQualityThresholdSlider_ZoomValueChanged(object sender, ZoomEventArgs e)
        {
            _extractor.FaceQualityThreshold = Convert.ToByte(e.ZoomValue * 100);
            Settings settings = Settings.Default;
            settings.ExtractorFaceQuality = Convert.ToByte(e.ZoomValue * 100);
        }

        private void ListAllDevices()
        {
            cbCameras.Items.Clear();
            NDevice selectedItem = null;
            foreach (NDevice item in _deviceManager.Devices)
            {
                cbCameras.Items.Add(item);
                if (item.DisplayName == _lastSelectedCamera)
                    selectedItem = item;
            }
            if (selectedItem != null)
                cbCameras.SelectedItem = selectedItem;
            else if (cbCameras.Items.Count > 0)
                cbCameras.SelectedIndex = 0;
        }

        private void cbCameras_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbCameras.Items.Count == 0)
            {
                _camera = null;
                _lastSelectedCamera = string.Empty;
                return;
            }
            _camera = cbCameras.SelectedItem as NCamera;
            _lastSelectedCamera = _camera != null ? _camera.DisplayName : string.Empty;
            UpdateFormatList();
        }

        private void cbFormats_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isUpdatingFormatList) return;

            _pendingFormat = cbFormats.SelectedItem as NVideoFormat;
        }

        private bool isUpdatingFormatList = false;

        private void UpdateFormatList()
        {
            _pendingFormat = null;
            isUpdatingFormatList = true;
            cbFormats.BeginUpdate();
            cbFormats.Items.Clear();
            try
            {
                if (_camera != null)
                {
                    foreach (NMediaFormat format in _camera.GetFormats())
                    {
                        cbFormats.Items.Add(format);
                    }
                    NMediaFormat currentFormat = _camera.GetCurrentFormat();
                    if (currentFormat != null)
                    {
                        int index = cbFormats.Items.IndexOf(currentFormat);
                        if (index == -1)
                        {
                            cbFormats.Items.Add(currentFormat);
                        }
                        cbFormats.SelectedItem = currentFormat;
                    }
                }
            }
            finally
            {
                cbFormats.EndUpdate();
                cbFormats.Enabled = cbFormats.Items.Count > 0;
                isUpdatingFormatList = false;
            }
        }

        private bool createFaceRecord = false;

        private void btnCapture_Click(object sender, EventArgs e)
        {
            if (_enrolledTemplateList == null || IsCapturing)
                return;

            ShowStatusMessage("");
            capture();
            ProgramMode mode = ProgramMode.Verify;
            setMode(mode);
            setModeRadioButtons(mode);
            createFaceRecord = true;
        }

        private void capture()
        {
            if (System.Configuration.ConfigurationManager.AppSettings["Verify"] != "file")
            {
                if (_camera == null)
                {
                    LogLine("No camera set", true);
                    return;
                }

                try
                {
                    if (_pendingFormat != null)
                    {
                        _camera.SetCurrentFormat(_pendingFormat);
                        _pendingFormat = null;
                    }

                    _camera.StartCapturing();
                    UpdateFormatList();
                    startCapturing();
                }
                catch (Exception ex)
                {
                    ShowError(ex.Message);
                }
            }
            else
            {
                enrollFromImage(true);  // to view2
                score = Identify(_capturedTemplateList, _enrolledTemplateList);
                LogLine(string.Format("Face match details: score {0}.", score), true);
                backgroundWorker_RunWorkerCompleted(null, null);
            }
        }

        private bool IsCapturing
        {
            get { return backgroundWorker.IsBusy; }
        }

        List<NImage> capturedImages = new List<NImage>();
        private void ClearCapturedImages()
        {
            foreach (NImage img in capturedImages)
            {
                img.Dispose();
            }
            capturedImages.Clear();
        }

        void startCapturing()
        {
            cbCameras.Enabled = false;
            buttonRequest.Enabled = false;
            backgroundWorker.RunWorkerAsync();
        }

        private void stopCapturing()
        {
            if (IsCapturing)
            {
                backgroundWorker.CancelAsync();
            }
        }

        int score = 0;

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            bool extractionStarted = false;
            try
            {
                NImage frame = null;
                NGrayscaleImage grayscaleImage = null;
                int frameNumber = 0;
                int bestFrame;
                int frameCount = Tools.LiveEnrolFrameCount;
                _extractor.DetectAllFeaturePoints = false;

                while (backgroundWorker.CancellationPending == false)
                {
                    if (_pendingFormat != null && _fromCamera)
                    {
                        _camera.SetCurrentFormat(_pendingFormat);
                        _pendingFormat = null;
                    }

                    if (!_fromCamera && _pause)
                    {
                        System.Threading.Thread.Sleep(500);
                        continue;
                    }

                    try
                    {
                        TimeSpan duration = TimeSpan.Zero;
                        TimeSpan timeStamp = TimeSpan.Zero;

                        if (_fromCamera)
                        {
                            frame = _camera.GetFrame();
                        }
                        else
                        {
                            lock (_readerLock)
                            {
                                frame = _videoReader.ReadVideoSample(out timeStamp, out duration);
                            }
                        }

                        if (frame == null) //camera unplugged or end of file
                        {
                            createFaceRecord = false;
                            SetImageToView(null, null, null, NleExtractionStatus.None, -1, timeStamp);
                            return;
                        }

                        using (grayscaleImage = frame.ToGrayscale())
                        {
                            if (createFaceRecord)
                            {
                                NleDetectionDetails details;
                                NLTemplate template = null;
                                //NBuffer template = null;
                                if (!extractionStarted)
                                {
                                    UpdateExtractorTemplateSize();
                                    frameCount = Tools.LiveEnrolFrameCount;
                                    _extractor.ExtractStart();
                                    extractionStarted = true;
                                    frameNumber = 0;
                                    ClearCapturedImages();
                                }
                                frameNumber++;
                                NleExtractionStatus status = _extractor.ExtractNext(grayscaleImage, out details);
                                capturedImages.Add((NImage)frame.Clone());

                                if (status != NleExtractionStatus.None || frameNumber >= frameCount)
                                {
                                    template = _extractor.ExtractEnd(out bestFrame, out status);
                                    if (status == NleExtractionStatus.TemplateCreated)
                                    {
                                        NTemplate nTemplate = new NTemplate();
                                        NImage bestImage = frame;
                                        if (bestFrame < capturedImages.Count && bestFrame >= 0) bestImage = capturedImages[bestFrame];
                                        _newRecord = new FaceRecord(template, bestImage, details);
                                        _newRecord.AddToTemplate(nTemplate);
                                        template.Dispose();
                                        capturedImages.Remove(bestImage);
                                        _capturedTemplateList = new List<byte[]>();
                                        _capturedTemplateList.Add(nTemplate.Save().ToByteArray());

                                        score = Identify(_capturedTemplateList, _enrolledTemplateList);
                                        LogLine(string.Format("Face match details: score {0}.", score), true);

                                        backgroundWorker.CancelAsync();
                                    }
                                    else
                                    {
                                        _newRecord = null;
                                    }
                                    extractionStarted = false;
                                    createFaceRecord = false;
                                }

                                if (!createFaceRecord)
                                {
                                    ClearCapturedImages();
                                }

                                SetImageToView(nlView2, frame.ToBitmap(), new NleDetectionDetails[] { details }, status, (int)(frameNumber * 100.0 / frameCount), timeStamp);

                                if (status != NleExtractionStatus.None && status != NleExtractionStatus.TemplateCreated)
                                {
                                    backgroundWorker.CancelAsync();

                                    score = 0;
                                }
                            }
                            else
                            {
                                NleDetectionDetails[] details = null;
                                try
                                {
                                    NleFace[] faces = _extractor.DetectFaces(grayscaleImage);
                                    if (faces != null)
                                    {
                                        details = new NleDetectionDetails[faces.Length];
                                        for (int i = 0; i < faces.Length; i++)
                                        {
                                            details[i] = _extractor.DetectFacialFeatures(grayscaleImage, faces[i]);
                                        }
                                    }
                                }
                                finally
                                {
                                    SetImageToView(nlView2, frame.ToBitmap(), details, NleExtractionStatus.None, -1, timeStamp);
                                }
                            }
                        }//using
                    }// try
                    finally
                    {
                        if (frame != null) frame.Dispose();
                    }
                }// while
            }
            catch (Exception ex)
            {
                foreach (NImage img in capturedImages)
                {
                    img.Dispose();
                }
                capturedImages.Clear();

                ShowError(ex.Message);
            }
            finally
            {
                try
                {
                    int baseFrameIndex;
                    NleExtractionStatus status;
                    if (extractionStarted) _extractor.ExtractEnd(out baseFrameIndex, out status);
                    if (_fromCamera && _camera != null) _camera.StopCapturing();
                    if (!_fromCamera && _videoReader != null) _videoReader.Stop();
                }
                catch { }
            }
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_capturedTemplateList != null)
                _capturedTemplateList.Clear();

            showScore(score);

            cbCameras.Enabled = true;
            buttonRequest.Enabled = true;
        }

        private delegate void showScoreDelegate(int score);
        private void showScore(int score)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new showScoreDelegate(showScore), score);
                return;
            }
            
            if (score > 0)
                pictureBox2.Image = Properties.Resources.checkmark;
            else
                pictureBox2.Image = Properties.Resources.redcross;
        }

        private delegate int IdentifyDelegate(List<NImage> capturedTemplateList, List<byte[]> enrolledTemplateList);
        private int Identify(List<byte[]> capturedTemplateList, List<byte[]> enrolledTemplateList)
        {
            NMatcher matcher = new NMatcher();
            //List<MatchingResult> identifiedIDs = new List<MatchingResult>();
            NMatchingDetails details = null;

            matcher.Verify(enrolledTemplateList[0], capturedTemplateList[0], out details);

            return details.FacesScore;
        }

        private void UpdateExtractorTemplateSize()
        {
            if (_mode == ProgramMode.Enroll)
            {
                try
                {
                    _extractor.TemplateSize = Settings.Default.EnrollTemplateSize;
                }
                catch
                {
                    _extractor.TemplateSize = NleTemplateSize.Large;
                }
            }
            else
            {
                try
                {
                    _extractor.TemplateSize = Settings.Default.IdentificationTemplateSize;
                }
                catch
                {
                    _extractor.TemplateSize = NleTemplateSize.Large;
                }
            }

        }

        private void devMan_DeviceRemoved(object sender, NDeviceManagerDeviceEventArgs e)
        {
            if (cbCameras.InvokeRequired)
            {
                cbCameras.Invoke(new OnDeviceChange(devMan_DeviceRemoved), sender, e);
            }
            else
            {
                cbCameras.Items.Remove(e.Device);
                if (_camera == e.Device)
                {
                    _camera = null;
                    cbFormats.Items.Clear();
                }
            }
        }

        private void devMan_DeviceAdded(object sender, NDeviceManagerDeviceEventArgs e)
        {
            if (cbCameras.InvokeRequired)
            {
                cbCameras.Invoke(new OnDeviceChange(devMan_DeviceAdded), sender, e);
            }
            else
            {
                ListAllDevices();
            }
        }

        private void Clear()
        {
            ShowStatusMessage("");
            ClearView();
            ClearLog();
        }

        private void ClearView()
        {
            pictureBox2.Image = null;
        }

        private void OnImage(NGrayscaleImage grayscaleImage)
        {
            switch (_mode)
            {
                case ProgramMode.Enroll:
                    //                       doEnroll(this.userId, image);
                    doEnroll();
                    //nfView2.Zoom = 1F;
                    break;
                case ProgramMode.Verify:
                    //                        doVerify(this.userId);
                    doVerify();
                    //nfView2.Zoom = 0.5F;
                    break;
            }

            //WaitingForImageToScan();
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
            ProgramMode mode = ProgramMode.Verify;
            setMode(mode);
            setModeRadioButtons(mode);

            this.BeginInvoke(new MethodInvoker(delegate() { startCapturing(); }));
        }

        private void doVerify()
        {
            int score;

            try
            {
                score = Data.NMatcher.Verify(Template.Save(), record.Template, out matchDetails);
            }
            catch (Exception ex)
            {
                string text = string.Format("Error verifying templates: {0}", ex.Message);
                ShowError(text);

                LogLine(string.Format("Error verifying templates: {0}", ex.Message), true);

                pictureBox2.Image = Properties.Resources.redcross;

                return;
            }

            string str = "";

            LogLine(str, true);

            ShowStatusMessage(str);

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

        private void ClearLog()
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

            _mode = mode;
        }

        private void FaceForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            stopCapturing();
            _deviceManager.DeviceAdded -= new EventHandler<NDeviceManagerDeviceEventArgs>(devMan_DeviceAdded);
            _deviceManager.DeviceRemoved -= new EventHandler<NDeviceManagerDeviceEventArgs>(devMan_DeviceRemoved);
        }

        private void radioButtonGroup_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton)
            {
                RadioButton radiobutton = sender as RadioButton;
                switch (radiobutton.Text)
                {
                    case "Enroll":
                        _mode = ProgramMode.Enroll;
                        break;
                    case "Verify":
                        _mode = ProgramMode.Verify;
                        break;
                    case "Identify":
                        _mode = ProgramMode.Identify;
                        break;
                }
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
            //this.BeginInvoke(new MethodInvoker(delegate() { stopCapturing(); }));

            ProgramMode mode = ProgramMode.Enroll;
            setMode(mode);
            setModeRadioButtons(mode);

            if (!isUserIdValid())
                return;

            nlView2.Image = null;

            if (_enrolledTemplateList != null)
                _enrolledTemplateList.Clear();
            
            Clear();


            //startProgressBar();
            Application.DoEvents();

            enrollFromImage(false);   // to view1

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
            this.InvokeOnClick(rb, new EventArgs());
        }

        private bool isUserIdValid()
        {
            switch (_mode)
            {
                case ProgramMode.Enroll:
                case ProgramMode.Verify:
                    if (!Int32.TryParse(personId.Text, out this.userId))
                    {
                        ResourceManager rm = new ResourceManager("PSCBioVerification.Form1", this.GetType().Assembly);
                        string text = rm.GetString("msgEnterPersonId"); // "Enter Person Id"
                        ShowError(text);
                        LogLine(text, true);
                        return false;
                    }
                    break;
            }

            return true;
        }


        void ShowStatusMessage(string message)
        {
            toolStripStatusLabelError.ForeColor = Color.Black;
            toolStripStatusLabelError.Text = message;
            Application.DoEvents();
        }

        void ShowError(string message)
        {
            Application.DoEvents();

            toolStripStatusLabelError.ForeColor = Color.Red;
            toolStripStatusLabelError.Text = message;
            LogLine(message, true);
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
}
