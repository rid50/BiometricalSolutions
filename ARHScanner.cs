using System;
using gx;
using pr;
using fps;
using System.Collections;
using System.IO;
using System.Runtime.Serialization;

class ARHScanner
{
    PassportReader _pr = null;
    FingerPrintScanner _fps = null;

    //Lib _lib;
    Helper _helper = null;
    prDoc _doc;

    string _errorMessage;
    public string ErrorMessage
    {
        get { return _errorMessage; }
        set { _errorMessage = value; }
    }

    public int DeviceState { get; set; }

    private IList _arrayOfWSQ = null;
    public IList ArrayOfWSQ
    {
        get
        {
            return _arrayOfWSQ;
        }
    }

    private IList _arrayOfBMP = null;
    public IList ArrayOfBMP
    {
        get
        {
            return _arrayOfBMP;
        }
    }

    private byte[] _nistImageBytes = null;
    public byte[] NistImageBytes
    {
        get
        {
            return _nistImageBytes;
        }
    }

    public int fpsConnect()
    {
        if (_helper == null)
            _helper = new Helper();

        try
        {
            fpsDisconnect();

            /* Opening the FPS system */
            _fps = new FingerPrintScanner();	/* Object for the FPS system */
/*
            int i = _fps.TestPowerState();

            if (_fps.TestPowerState() != 0)
            {
                ErrorMessage = "The power is off";
                return -1;
            }
*/
            /* Validity check */
            if (!_fps.IsValid())
            {
                ErrorMessage = "Failed to initialize!";
                return 1303;
            }

            /* Connecting to the first device */
            _fps.UseDevice(0, (int)FPS_USAGEMODE.FPS_UMODE_FULL_CONTROL);

            //int i = _fps.TestPowerState();

        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- fpsConnect()";
            return 1305;
        }

        return 0;
    }

    public int fpsGetFingersImages(int handAndFingerMask, bool saveFingerAsFile)
    {
        try
        {
            if (_arrayOfBMP != null) { _arrayOfBMP.Clear(); _arrayOfBMP = null; }
            if (_arrayOfWSQ != null) { _arrayOfWSQ.Clear(); _arrayOfWSQ = null; }

            /* Search Finger */
            int reqid, stat;

            /* Clears internal stored finger buffers */
            _fps.ResetFingerList();


            /* Starts an asynchronous capture process
            // params: time in usec, quality in per-thousand, mode of live scan, fingerlist
            //
            // The finger list has the format 0hhh 0000 iiii mmmm rrrr llll tttt ssss
            //	h - scan object: 001 left hand, 010 right hand, 011 same fingers of both hands
            //	i - index finger	|
            //	m - middle finger	|
            //	r - ring finger		|--> value of FPS_PRESENCE   FPS_AVAILABLE = 3 
            //	l - little finger	|
            //	t - left thumb		|
            //	s - right thumb		|
            */

            int color = (int)FPS_STATUS_LED_COLOR.FPS_SLC_OFF;
            int index = 0;
            int middle = 0;
            int ring = 0;
            int little = 0;
            string wsqIndexFileName = String.Empty;
            string wsqMiddleFileName = String.Empty;
            string wsqRingFileName = String.Empty;
            string wsqLittleFileName = String.Empty;

            _fps.SetStatusLed(0xff, color); // off
            color = (int)FPS_STATUS_LED_COLOR.FPS_SLC_GREEN;

            int fingerMask = 0x00;
            switch (handAndFingerMask & 0xff000000)
            {
                case 0x10000000:    //0x10333300    left hand
                case 0x20000000:    //0x20333300    right hand
                    fingerMask |= (handAndFingerMask & 0x00300000) != 0 ? 0x08 : 0x00;
                    fingerMask |= (handAndFingerMask & 0x00030000) != 0 ? 0x04 : 0x00;
                    fingerMask |= (handAndFingerMask & 0x00003000) != 0 ? 0x02 : 0x00;
                    fingerMask |= (handAndFingerMask & 0x00000300) != 0 ? 0x01 : 0x00;
                    break;
                case 0x30000000:    //0x20000033    thumbs
                    fingerMask |= (handAndFingerMask & 0x00000030) != 0 ? 0x04 : 0x00;
                    fingerMask |= (handAndFingerMask & 0x00000003) != 0 ? 0x02 : 0x00;
                    break;
            }

            int lampMask = fingerMask;
            switch (handAndFingerMask & 0xff000000)
            {
                case 0x10000000:    //0x10333300  left hand
                    index = (int)FPS_POSITION.FPS_POS_LEFT_INDEX;
                    middle = (int)FPS_POSITION.FPS_POS_LEFT_MIDDLE;
                    ring = (int)FPS_POSITION.FPS_POS_LEFT_RING;
                    little = (int)FPS_POSITION.FPS_POS_LEFT_LITTLE;
                    wsqIndexFileName = "lindex.wsq";
                    wsqMiddleFileName = "lmiddle.wsq";
                    wsqRingFileName = "lring.wsq";
                    wsqLittleFileName = "llittle.wsq";
                    lampMask = 0x80;
                    lampMask |= (fingerMask & 0x00000001) != 0 ? 0x08 : 0x00;
                    lampMask |= (fingerMask & 0x00000002) != 0 ? 0x04 : 0x00;
                    lampMask |= (fingerMask & 0x00000004) != 0 ? 0x02 : 0x00;
                    lampMask |= (fingerMask & 0x00000008) != 0 ? 0x01 : 0x00;
                    break;
                case 0x20000000:    //0x20333300
                    index = (int)FPS_POSITION.FPS_POS_RIGHT_INDEX;
                    middle = (int)FPS_POSITION.FPS_POS_RIGHT_MIDDLE;
                    ring = (int)FPS_POSITION.FPS_POS_RIGHT_RING;
                    little = (int)FPS_POSITION.FPS_POS_RIGHT_LITTLE;
                    wsqIndexFileName = "rindex.wsq";
                    wsqMiddleFileName = "rmiddle.wsq";
                    wsqRingFileName = "rring.wsq";
                    wsqLittleFileName = "rlittle.wsq";
                    lampMask |= 0x40;
                    break;
                case 0x30000000:    //0x30000033
                    middle = (int)FPS_POSITION.FPS_POS_LEFT_THUMB;
                    ring = (int)FPS_POSITION.FPS_POS_RIGHT_THUMB;
                    wsqMiddleFileName = "lthumb.wsq";
                    wsqRingFileName = "rthumb.wsq";
                    lampMask |= 0x20;
                    break;
            }

            /* Turning the display leds depending on the mask */
            _fps.SetStatusLed(lampMask, color);

            //reqid = _fps.CaptureStart(100, 100, (int)FPS_IMPRESSION_TYPE.FPS_SCAN_LIVE, 0x10333300);
            reqid = _fps.CaptureStart(3000, 7000, (int)FPS_IMPRESSION_TYPE.FPS_SCAN_LIVE, handAndFingerMask);

            for (stat = 0; stat < 100; )
            {
                /* Test if better images are captured or capture has accomplished */
                stat = _fps.CaptureStatus(reqid);

                _helper.Wait(100);
            }

            /* Closing the capture sequence */
            _fps.CaptureWait(reqid);

            color = (int)FPS_STATUS_LED_COLOR.FPS_SLC_OFF;
            _fps.SetStatusLed(0xff, color); // off

            /* Save individual finger images */
            gxImage img;
            gxVariant var = null;
            int mask = 0x10;

            _arrayOfBMP = new ArrayList();
            _arrayOfWSQ = new ArrayList();

            for (int i = 0; i < 4; i++)
            {
                mask >>= 1; 
                bool valid = true;
                switch (fingerMask & mask)
                {
                    case 0x08:
                        try
                        {
                            var = _fps.GetImage((int)FPS_IMPRESSION_TYPE.FPS_SCAN_LIVE, index, (int)FPS_IMAGE_TYPE.FPS_IT_FINGER);
                            if (saveFingerAsFile)
                                _fps.SaveImage(0, index, (int)FPS_IMAGE_TYPE.FPS_IT_FINGER, wsqIndexFileName, (int)GX_IMGFILEFORMATS.GX_WSQ);
                        }
                        catch
                        {
                            valid = false;
                            if (saveFingerAsFile)
                                File.Delete(wsqIndexFileName);
                        }
                        break;
                    case 0x04:
                        try
                        {
                            var = _fps.GetImage((int)FPS_IMPRESSION_TYPE.FPS_SCAN_LIVE, middle, (int)FPS_IMAGE_TYPE.FPS_IT_FINGER);
                            if (saveFingerAsFile)
                                _fps.SaveImage(0, middle, (int)FPS_IMAGE_TYPE.FPS_IT_FINGER, wsqMiddleFileName, (int)GX_IMGFILEFORMATS.GX_WSQ);
                        }
                        catch
                        {
                            valid = false;
                            if (saveFingerAsFile)
                                File.Delete(wsqMiddleFileName);
                        }
                        break;
                    case 0x02:
                        try
                        {
                            var = _fps.GetImage((int)FPS_IMPRESSION_TYPE.FPS_SCAN_LIVE, ring, (int)FPS_IMAGE_TYPE.FPS_IT_FINGER);
                            if (saveFingerAsFile)
                                _fps.SaveImage(0, ring, (int)FPS_IMAGE_TYPE.FPS_IT_FINGER, wsqRingFileName, (int)GX_IMGFILEFORMATS.GX_WSQ);
                        }
                        catch
                        {
                            valid = false;
                            if (saveFingerAsFile)
                                File.Delete(wsqRingFileName);
                        }
                        break;
                    case 0x01:
                        try
                        {
                            var = _fps.GetImage((int)FPS_IMPRESSION_TYPE.FPS_SCAN_LIVE, little, (int)FPS_IMAGE_TYPE.FPS_IT_FINGER);
                            if (saveFingerAsFile)
                                _fps.SaveImage(0, little, (int)FPS_IMAGE_TYPE.FPS_IT_FINGER, wsqLittleFileName, (int)GX_IMGFILEFORMATS.GX_WSQ);
                        }
                        catch
                        {
                            valid = false;
                            if (saveFingerAsFile)
                                File.Delete(wsqLittleFileName);
                        }
                        break;
                    default:
                        _arrayOfBMP.Add(new Byte[] { new Byte() });
                        _arrayOfWSQ.Add(new WsqImage());
//                        _arrayOfWSQ.Add(new Byte[] { new Byte() });
                        continue;
                }

                if (valid)
                {
                    img = new gxImage();
                    gxVariant vtest = new gxVariant();
                    img.FromVariant(var);

                    WsqImage im = new WsqImage();
                    im.Content = img.SaveToMem((int)GX_IMGFILEFORMATS.GX_WSQ);
                    //im.Content = img.SaveToMem((int)GX_IMGFILEFORMATS.GX_BMP);
                    im.XRes = img.xres() / (10000 / 254);
                    im.YRes = img.yres() / (10000 / 254);
                    im.XSize = img.xsize();
                    im.YSize = img.ysize();
                    im.PixelFormat = img.format();
                    _arrayOfWSQ.Add(im);

                    _arrayOfBMP.Add(img.SaveToMem((int)GX_IMGFILEFORMATS.GX_BMP));
                    //_arrayOfWSQ.Add(img.SaveToMem((int)GX_IMGFILEFORMATS.GX_WSQ));
                    img.Dispose();
                }
                else
                {
                    //list.Add(new Byte[] { new Byte() });
                    _arrayOfBMP.Add(null);
                    _arrayOfWSQ.Add(null);
                }

                if (var != null)
                    var.Dispose();
            }
        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- fpsGetFingersImages()";
            return 1305;
        }

        return 0;
    }

    public int fpsGetNist(bool saveFingersAsFile)
    {
        _nistImageBytes = null;
        try
        {
            /* This section modifies the values of nist record */

            gxVariant v = new gxVariant();
            gxVariant v1 = new gxVariant();
            gxVariant v2 = new gxVariant();

            v.CreateEmptyList(0);	/* General list */

            v1.CreateEmptyList(1);		/* List for storing the type-1 record data */
            v.AddItem((int)GX_VARIANT_FLAGS.GX_VARIANT_LAST, 0, 0, v1);

            v2.Create(4, "ATP");	/* (field id) - (field value) */
            v1.AddItem((int)GX_VARIANT_FLAGS.GX_VARIANT_LAST, 0, 0, v2);

            /* Saves all the captured fingers */
            if (saveFingersAsFile)
                _fps.FingerToNist("mynist.nist", v);

            _nistImageBytes = _fps.FingerToNistMem(v);
        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- fpsGetFingersToNist()";
            return 1305;
        }

        return 0;
    }

    public int prConnect()
    {
        if (_helper == null)
            _helper = new Helper();

        try
        {
            /* Opening the PR system */
            _pr = new PassportReader();	/* Object for the PR system */
/*
            if (_pr.TestPowerState() != 0)
            {
                ErrorMessage = "The power is off";
                return -1;
            }
*/
            /* Validity check */
            if (!_pr.IsValid())
            {
                ErrorMessage = "Failed to initialize!";
                return 1303;
            }

            /* Connecting to the first device */
            _pr.UseDevice(0, (int)PR_USAGEMODE.PR_UMODE_FULL_CONTROL);

        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prConnect()";
            return 1305;
        }

        return 0;
    }

    public int prListen()
    {
        /* Enabling motion detection */
        try
        {
            _pr.SetProperty("freerun_mode", (int)PR_FREERUNMODE.PR_FRMODE_TESTDOCUMENT);
        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }

        try
        {
            /* If the start button is not pressed testing the document detection */
            int state = _pr.TestDocument(0);

            /* Turning the display leds depending on the status */
            int color = (int)PR_STATUS_LED_COLOR.PR_SLC_OFF;
            switch (state)
            {
                case (int)PR_TESTDOC.PR_TD_OUT: color = (int)PR_STATUS_LED_COLOR.PR_SLC_GREEN; break;
                case (int)PR_TESTDOC.PR_TD_MOVE: color = (int)PR_STATUS_LED_COLOR.PR_SLC_ANY; break;
                case (int)PR_TESTDOC.PR_TD_NOMOVE: color = (int)PR_STATUS_LED_COLOR.PR_SLC_RED; break;
            }
            _pr.SetStatusLed(0xff, color);

            DeviceState = state;

            _helper.Wait(200);
        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prListen()";
            return 1305;
        }

        return 0;
    }

    public int prCaptureImage()
    {
        try
        {
            /* Capturing images */
            _pr.Capture();
        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prCaptureImage()";
            return 1305;
        }

        return 0;
    }

    public int prCaptureMRZ()
    {
        try
        {
            /* Capturing images */
            //_pr.Capture();

            /* Getting the MRZ data */
            _doc = _pr.GetMrz(0, (int)PR_LIGHT.PR_LIGHT_INFRA, (int)PR_IMAGE_TYPE.PR_IT_ORIGINAL);

            if (!_doc.IsValid())
                throw new NoDocumentFoundException("No MRZ data found");
        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (NoDocumentFoundException e)
        {
            _errorMessage = e.Message;
            return 1303;
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prCaptureMRZ()";
            return 1305;
        }

        return 0;
    }

    public int prGetMRZData()
    {
        return 0;
    }

    public int prGetMRZData(System.Collections.IList list)
    {
        try
        {
            if (_doc.IsValid())
            {
                string fieldName, text;
                int j = "PR_DF_MRZ_".Length;
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (int i in Enum.GetValues(typeof(PR_DOCFIELD)))
                {
                    if (i <= (int)PR_DOCFIELD.PR_DF_MRZ_FIELDS)
                        continue;

                    fieldName = Enum.GetName(typeof(PR_DOCFIELD), i);
                    if (fieldName.StartsWith("PR_DF_MRZ_"))
                    {
                        text = _doc.Field(i);
                        text = text.Replace('<', ' ').Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            text = fieldName.Substring(j).Replace('_', ' ') + ": " + text;
                            list.Add(text);
                        }
                    }
                }
            }
        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prGetMRZData()";
            return 1305;
        }

        return 0;
    }

    public int prCaptureBarcode()
    {
        try
        {
            /* Capturing images */
            //_pr.Capture();

            /* Reading barcode from infra image */
            _doc = _pr.GetBarcode(0, (int)PR_LIGHT.PR_LIGHT_INFRA, (int)PR_IMAGE_TYPE.PR_IT_ORIGINAL, 0, 0);

            if (!_doc.IsValid())
            {
                /* Reading barcode from white image */
                _doc = _pr.GetBarcode(0, (int)PR_LIGHT.PR_LIGHT_WHITE, (int)PR_IMAGE_TYPE.PR_IT_ORIGINAL, 0, 0);

                //                bool statusOk = _doc.FieldStatus((int)PR_DOCFIELD.PR_DF_BC1) == 0;
                //              if (!statusOk)
                //                _doc = _pr.GetBarcode(0, (int)PR_LIGHT.PR_LIGHT_UV, (int)PR_IMAGE_TYPE.PR_IT_ORIGINAL, 0, 0);
            }

            if (!_doc.IsValid())
                throw new NoDocumentFoundException("No barcode found");
        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (NoDocumentFoundException e)
        {
            _errorMessage = e.Message;
            return 1303;
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prCaptureBarcode()";
            return 1305;
        }

        return 0;
    }

    public int prGetGTIN()
    {
        return 0;
    }

    public int prGetGTIN(out string gtin, out string barcodeType)
    {
        gtin = ""; barcodeType = "";

        try
        {
            if (_doc.IsValid())
            {
                int type = -1;

                gxVariant pdoc = _doc.ToVariant();
                gxVariant v = new gxVariant();
                if (pdoc.GetChild(v, (int)GX_VARIANT_FLAGS.GX_VARIANT_BY_ID, (int)PR_VAR_ID.PRV_BARCODE, 0))
                {
                    type = v.GetInt();
                    v.Dispose();
                }

                barcodeType = System.Enum.GetName(typeof(PR_BCTYPE), type);
                barcodeType = barcodeType.Substring(barcodeType.LastIndexOf("_") + 1);

                gtin = _doc.Field((int)PR_DOCFIELD.PR_DF_BC1) as string;
            }
        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prGetBarcodeData()";
            return 1305;
        }

        return 0;
    }

    public int prGetBarcodeData()
    {
        return 0;
    }

    public int prGetBarcodeData(System.Collections.IList list)
    {
        try
        {
            if (_doc.IsValid())
            {
                /* Searching for the barcode and displaying it */
                int type = -1;

                gxVariant pdoc = _doc.ToVariant();
                gxVariant v = new gxVariant();
                if (pdoc.GetChild(v, (int)GX_VARIANT_FLAGS.GX_VARIANT_BY_ID, (int)PR_VAR_ID.PRV_BARCODE, 0))
                {
                    type = v.GetInt();
                    v.Dispose();
                }

                string barcodeType = System.Enum.GetName(typeof(PR_BCTYPE), type);
                barcodeType = barcodeType.Substring(barcodeType.LastIndexOf("_") + 1);
                list.Add(String.Format("TYPE: {0}", barcodeType));      //barcode type
                list.Add(String.Format( // checksum
                    "CHECKSUM: {0}", _doc.FieldStatus((int)PR_DOCFIELD.PR_DF_BC1) == 0 ? "Ok" : "No checksum"));

                if (barcodeType == "PDF417")
                {
                    string fieldName, text;
                    int j = "PR_DF_".Length;
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    foreach (int i in Enum.GetValues(typeof(PR_DOCFIELD)))
                    {
                        if (i <= (int)PR_DOCFIELD.PR_DF_FORMATTED)
                            continue;

                        fieldName = Enum.GetName(typeof(PR_DOCFIELD), i);
                        if (fieldName.StartsWith("PR_DF_"))
                        {
                            text = _doc.Field(i).Trim();
                            if (!string.IsNullOrEmpty(text))
                            {
                                text = fieldName.Substring(j).Replace('_', ' ') + ": " + text;
                                list.Add(text);
                            }
                        }
                    }
                }
                else
                    list.Add("DATA: " + _doc.Field((int)PR_DOCFIELD.PR_DF_BC1) as string);
            }
        }
        catch (gxException e)
        {
            return _helper.GetErrorMessage(e, out _errorMessage);
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prGetBarcodeData()";
            return 1305;
        }

        return 0;
    }

    public int prGetBarcodeImage()
    {
        return 0;
    }

    public int prGetBarcodeImage(out byte[] buff)
    {
        buff = null;

        try
        {
            if (_doc.IsValid())
            {
                /* Creating a barcode image */
                gxImage img = _doc.FieldImage((int)PR_DOCFIELD.PR_DF_BC1);
                if (img.IsValid())
                {
                    buff = img.SaveToMem((int)GX_IMGFILEFORMATS.GX_JPEG);
                }
            }
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prGetBarcodeImage()";
            return 1305;
        }

        return 0;
    }

    public int prGetMRZImage()
    {
        return 0;
    }

    public int prGetMRZImage(out byte[] buff)
    {
        buff = null;

        try
        {
            if (_doc.IsValid())
            {
                /* Creating a MRZ image */
                gxImage img = _doc.FieldImage((int)(PR_DOCFIELD.PR_DF_MRZ1 & PR_DOCFIELD.PR_DF_MRZ2));
                if (img.IsValid())
                {
                    buff = img.SaveToMem((int)GX_IMGFILEFORMATS.GX_JPEG);
                }
            }
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prGetMRZImage()";
            return 1305;
        }

        return 0;
    }

    public int prSaveBarcodeImage()
    {
        try
        {
            if (_doc.IsValid())
            {
                /* Saving the barcode image */
                gxImage img = _doc.FieldImage((int)PR_DOCFIELD.PR_DF_BC1);
                if (img.IsValid())
                    img.Save("barcode.jpg", (int)GX_IMGFILEFORMATS.GX_JPEG);

                //_doc.Free();
                //_doc = null;
                //_pr.ResetDocument();
            }
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prSaveBarcodeImage()";
            return 1305;
        }

        return 0;
    }

    public int prSaveMRZImage()
    {
        try
        {
            if (_doc.IsValid())
            {
                /* Saving the MRZ image */
                gxImage img = _doc.FieldImage((int)(PR_DOCFIELD.PR_DF_MRZ1 & PR_DOCFIELD.PR_DF_MRZ2));
                if (img.IsValid())
                    img.Save("mrz.jpg", (int)GX_IMGFILEFORMATS.GX_JPEG);

                //_doc.Free();
                //_doc = null;
                //_pr.ResetDocument();
            }
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prSaveMRZImage()";
            return 1305;
        }

        return 0;
    }

    public int prReleaseDocument()
    {
        try
        {
            if (_doc != null && _doc.IsValid())
            {
                _doc.Free();
                _doc = null;
            }
        }
        catch (Exception e)
        {
            _errorMessage = e.Message + " --- prReleaseDocument()";
            return 1305;
        }

        return 0;
    }

    public int fpsDisconnect()
    {
        /* Closing the device */
        while (_fps != null)
        {
            try
            {
                _fps.CloseDevice();
                _fps.Dispose();
                _fps = null;

                break;
            }
            catch (Exception)
            {
                //if (gxSystem.GetErrorCode() == (int)GX_ERROR_CODES.GX_EBUSY)
                continue;
            }
        }
        return 0;
    }

    public int prDisconnect()
    {
        /* Closing the device */
        while (_pr != null)
        {
            try
            {
                if (_doc != null)
                    _doc.Free();

                _pr.ResetDocument();

                _pr.CloseDevice();
                _pr.Dispose();
                _pr = null;

                break;
            }
            catch (Exception)
            {
                //if (gxSystem.GetErrorCode() == (int)GX_ERROR_CODES.GX_EBUSY)
                continue;
            }
        }
        return 0;
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

