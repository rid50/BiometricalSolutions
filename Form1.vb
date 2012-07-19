Imports System.Text
Imports System.IO
Imports System.Media
Imports System.Drawing.Printing
Imports System.Drawing.Text
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Formatters.Binary

Public Class Form1
    Dim _pr As New pscpr.PassportReader()
    Dim _fps As New pscpr.FingerScanner()

    Private Sub Form1_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        Dim pb As PictureBox
        Dim cb As CheckBox
        Dim i As Integer

        Panel1.Visible = True

        For i = 1 To 11
            If i = 9 Then
                Continue For
            End If

            pb = Me.Controls.Find("fpPictureBox" + i.ToString(), True)(0)
            pb.Image = Nothing

            AddHandler pb.MouseHover, AddressOf fpPictureBox_MouseHover
            AddHandler pb.MouseLeave, AddressOf fpPictureBox_MouseLeave

            cb = Me.Controls.Find("checkBox" + i.ToString(), True)(0)
            cb.Checked = True
            TextBoxID.Focus()
        Next
    End Sub

    Private Sub button1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles button1.Click
        Dim errorCode As Integer
        Dim img As Image = Nothing
        Dim ms As IO.MemoryStream = Nothing

        toolStripStatusLabelError.Text = "Image processing..."
        toolStripStatusLabelError.ForeColor = Color.Black

        textBox1.Text = ""
        pictureBox1.Image = Nothing

        System.Windows.Forms.Application.DoEvents()

        errorCode = _pr.connect()
        If errorCode <> 0 Then
            toolStripStatusLabelError.Text = _pr.ErrorMessage
            toolStripStatusLabelError.ForeColor = Color.Red
            Return
        End If

        errorCode = _pr.readBarcode()
        If errorCode <> 0 Then
            toolStripStatusLabelError.Text = _pr.ErrorMessage
            toolStripStatusLabelError.ForeColor = Color.Red
            _pr.disconnect()
            Return
        End If

        Dim list As IList = _pr.Data

        If list IsNot Nothing Then
            textBox1.Text = ""
            For Each str As String In list
                textBox1.Text += str
                textBox1.Text += Environment.NewLine
            Next
        End If

        If _pr.BarcodeImageBytes IsNot Nothing Then
            ms = New MemoryStream(_pr.BarcodeImageBytes)
            img = Image.FromStream(ms)

            pictureBox1.Image = img
        End If

        _pr.disconnect()

        toolStripStatusLabelError.Text = ""
        tabControl1.SelectTab("tabPage1")

    End Sub


    Private Sub Button2_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button2.Click
        Dim errorCode As Integer
        Dim img As Image = Nothing
        Dim ms As MemoryStream = Nothing

        'TextBoxID.Enabled = False

        toolStripStatusLabelError.Text = "Image processing..."
        toolStripStatusLabelError.ForeColor = Color.Black

        textBox2.Text = ""
        pictureBox2.Image = Nothing

        System.Windows.Forms.Application.DoEvents()

        errorCode = _pr.connect()
        If errorCode <> 0 Then
            toolStripStatusLabelError.Text = _pr.ErrorMessage
            toolStripStatusLabelError.ForeColor = Color.Red
            Return
        End If

        errorCode = _pr.readMRZ()
        If errorCode <> 0 Then
            toolStripStatusLabelError.Text = _pr.ErrorMessage
            toolStripStatusLabelError.ForeColor = Color.Red
            _pr.disconnect()
            Return
        End If

        Dim list As IList = _pr.Data

        If list IsNot Nothing Then
            textBox2.Text = ""
            For Each str As String In list
                textBox2.Text += str
                textBox2.Text += Environment.NewLine
            Next
        End If

        If _pr.MRZImageBytes IsNot Nothing Then
            ms = New MemoryStream(_pr.MRZImageBytes)
            img = Image.FromStream(ms)

            pictureBox2.Image = img
        End If

        _pr.disconnect()
        toolStripStatusLabelError.Text = ""
        tabControl1.SelectTab("tabPage2")
    End Sub

    Dim _fingersCollection As ArrayList = Nothing

    Private Sub button4_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles button4.Click
        Dim errorCode As Integer
        Dim hand As Integer = 0
        Dim count As Integer = 3
        Dim offset As Integer = 0   ' fingers collection offset

        'TextBoxID.Enabled = True

        Dim id As Integer

        If (TextBoxID.Text.Length = 0 OrElse Not Int32.TryParse(TextBoxID.Text, id)) Then
            toolStripStatusLabelError.Text = "Please enter a valid ID"
            toolStripStatusLabelError.ForeColor = Color.Red
            Exit Sub
        Else
            toolStripStatusLabelError.Text = ""
        End If

        'saveWsqInDatabase()

        buttonLeftHand.Enabled = True
        buttonRightHand.Enabled = True
        buttonThumbs.Enabled = True

        If TypeOf e Is MyEventArgs Then
            Dim myEvent As MyEventArgs = TryCast(e, MyEventArgs)
            hand = myEvent.hand
            If hand = 0 Then        ' only left hand
                count = 1
            ElseIf hand = 1 Then    ' only right hand
                count = 2
                offset = 4
            Else                    ' only thumbs hand = 2; count = 3
                offset = 7          ' 7 !!! not 8 - this is because a pattern for thumbs is always 0110
            End If
        Else
            Dim pb As PictureBox
            Dim cb As CheckBox
            Dim i As Integer

            For i = 1 To 11
                If i = 9 Then
                    Continue For
                End If

                pb = Me.Controls.Find("fpPictureBox" + i.ToString(), True)(0)
                pb.Image = Nothing

                cb = Me.Controls.Find("checkBox" + i.ToString(), True)(0)
                cb.Checked = True
            Next
        End If

        tabControl1.SelectTab("tabPage3")
        Application.DoEvents()

        button4.Enabled = False

        toolStripStatusLabelError.Text = "Finger scan processing..."
        toolStripStatusLabelError.ForeColor = Color.Black

        Application.DoEvents()

        errorCode = _fps.connect()
        If errorCode <> 0 Then
            toolStripStatusLabelError.Text = _fps.ErrorMessage
            toolStripStatusLabelError.ForeColor = Color.Red
        Else

            If Not TypeOf e Is MyEventArgs Or _fingersCollection Is Nothing Then
                _fingersCollection = New ArrayList(10)
                Dim ii As Integer
                For ii = 0 To _fingersCollection.Capacity - 1
                    _fingersCollection.Add(Nothing)
                Next
            End If

            'Dim simpleSound As SoundPlayer = Nothing

            Dim i As Integer
            For i = hand To count - 1
                Select Case i
                    Case 0
                        label1.Text = "Put the left hand on the glass. "
                        'simpleSound = New SoundPlayer("left_hand.wav")
                        'simpleSound.Play()
                    Case 1
                        label1.Text = "Put the right hand on the glass. "
                        'simpleSound = New SoundPlayer("right_hand.wav")
                        'simpleSound.Play()
                    Case 2
                        label1.Text = "Put both thumbs on the glass. "
                        'simpleSound = New SoundPlayer("both_thumbs.wav")
                        'simpleSound.Play()
                End Select

                Application.DoEvents()

                System.Threading.Thread.Sleep(2000)

                label1.Text += "Go ..."
                Application.DoEvents()

                errorCode = scanFingers(i)
                If errorCode <> 0 Then
                    toolStripStatusLabelError.Text = _fps.ErrorMessage
                    toolStripStatusLabelError.ForeColor = Color.Red
                    Exit For
                End If

                Dim k As Integer
                For k = 0 To 3
                    If _fps.ArrayOfWSQ(k) IsNot Nothing AndAlso _fps.ArrayOfWSQ(k).Content IsNot Nothing Then
                        _fingersCollection(k + offset) = _fps.ArrayOfWSQ(k)
                    End If
                Next

                offset += 4
                If offset = 8 Then
                    offset = 7
                End If
            Next

            If errorCode = 0 Then
                toolStripStatusLabelError.Text = ""
                toolStripStatusLabelError.ForeColor = Color.Black

                Dim buff As Byte() = Nothing
                Dim ms As New MemoryStream()

                ' Construct a BinaryFormatter and use it to serialize the data to the stream.
                Dim formatter As New BinaryFormatter()
                Try
                    formatter.Serialize(ms, TryCast(_fingersCollection, ArrayList))
                    buff = ms.ToArray()
                    saveWsqInDatabase(id, buff)
                Catch ex As SerializationException
                    toolStripStatusLabelError.ForeColor = Color.Red
                    toolStripStatusLabelError.Text = ex.Message
                Finally
                    ms.Close()
                End Try
            End If
        End If

        label1.Text = ""
        _fps.disconnect()
        button4.Enabled = True
        button4.Focus()
    End Sub

    Private Function scanFingers(ByVal hand As Integer) As Integer  ' 0 - left hand;  1 - right hand;  2 - thumbs
        Dim errorCode As Integer = 0

        ' The finger list has the format 0hhh 0000 iiii mmmm rrrr llll tttt ssss
        '	h - scan object: 001 left hand, 010 right hand, 011 same fingers of both hands

        Dim sb As New StringBuilder(10)
        Dim pictureBoxOffset As Integer = 1
        Dim count As Integer = 4

        Select Case hand
            Case 0
                sb.Append("10")         '0x10333300    left hand
            Case 1
                sb.Append("20")         '0x20333300    right hand
                pictureBoxOffset = 5
            Case 2
                sb.Append("300000")     '0x30000033    thumbs
                pictureBoxOffset = 9
                count = 3
        End Select

        Dim j As Integer
        For j = 0 To count - 1
            If j + pictureBoxOffset = 9 Then
                Continue For
            End If

            Dim cb As CheckBox = TryCast(Me.Controls.Find("checkBox" + (j + pictureBoxOffset).ToString(), True)(0), CheckBox)
            If cb.Checked Then
                sb.Append("3")
            Else
                sb.Append("0")
            End If
        Next

        Select Case hand
            Case 0
                sb.Append("00")
            Case 1
                sb.Append("00")
        End Select

        Dim handAndFingerMask As Integer = Int32.Parse(sb.ToString(), System.Globalization.NumberStyles.AllowHexSpecifier)
        errorCode = _fps.getFingersImages(handAndFingerMask, True)
        If errorCode = 0 Then
            'saveWsqInDatabase()
            showFingers(pictureBoxOffset)
        End If

        scanFingers = errorCode
    End Function

    Enum SAVE
        INSERT = 0
        UPDATE = 1
    End Enum

    Private Sub saveWsqInDatabase(ByVal id As Integer, ByVal buff As Byte())
        Try
            Dim db As New DBUtil()
            db.SaveTemplate(SAVE.UPDATE, id, buff)
        Catch
        End Try
    End Sub


    Private Sub showFingers(ByVal pictureBoxOffset As Integer)
        Dim stream As MemoryStream = Nothing
        Dim buff As Byte()
        Dim i As Integer

        Dim pb As PictureBox
        Dim cb As CheckBox

        For i = 0 To 3
            buff = _fps.ArrayOfBMP(i)
            If buff IsNot Nothing Then
                If buff.Length = 1 Then
                    Continue For
                End If
            End If

            pb = Me.Controls.Find("fpPictureBox" + (i + pictureBoxOffset).ToString(), True)(0)
            If buff Is Nothing Then
                pb.Image = Nothing
            Else
                stream = New MemoryStream(buff)
                pb.Image = Image.FromStream(stream)
                pb.SizeMode = PictureBoxSizeMode.Zoom
                cb = Me.Controls.Find("checkBox" + (i + pictureBoxOffset).ToString(), True)(0)
                cb.Checked = False
            End If
        Next
    End Sub

    Private Class MyEventArgs
        Inherits EventArgs

        Public hand As Integer

        Public Sub New(ByVal hand As Integer)
            Me.hand = hand
        End Sub
    End Class

    Private Sub buttonLeftHand_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles buttonLeftHand.Click
        Me.InvokeOnClick(button4, New MyEventArgs(0))
    End Sub

    Private Sub buttonRightHand_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles buttonRightHand.Click
        Me.InvokeOnClick(button4, New MyEventArgs(1))
    End Sub

    Private Sub buttonThumbs_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles buttonThumbs.Click
        Me.InvokeOnClick(button4, New MyEventArgs(2))
    End Sub

    Private Sub Form1_FormClosed(ByVal sender As Object, ByVal e As System.Windows.Forms.FormClosedEventArgs) Handles Me.FormClosed
        _pr.disconnect()
        _fps.disconnect()
    End Sub

    Dim _helper As Helper = Nothing
    Dim _fontFamily As FontFamily = Nothing

    Private Sub generateBarcodeImage()
        If _helper Is Nothing Then
            _helper = New Helper()
        End If

        If _fontFamily Is Nothing Then
            ' Create a private font collection
            Dim pfc As New PrivateFontCollection()
            ' Load in the temporary barcode font
            pfc.AddFontFile("3OF9_NEW.TTF")
            ' Select the font family to use
            _fontFamily = New FontFamily("3 of 9 Barcode", pfc)
        End If

        Dim result As Integer
        If Not Int32.TryParse(textBox5.Text, result) Then
            result = 24
            textBox5.Text = result.ToString()
        End If

        pictureBox3.Image = _helper.GenerateBarcodeImage(textBox4.Text, _fontFamily, result)
    End Sub

    Private Sub button5_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles button5.Click
        If String.IsNullOrEmpty(textBox4.Text) Then
            Return
        End If

        generateBarcodeImage()

        ' Create a Print Document
        Dim doc As New PrintDocument()
        'doc.PrintPage += New PrintPageEventHandler(PrintPage)
        AddHandler doc.PrintPage, AddressOf PrintPage
        doc.PrinterSettings.PrinterName = "Microsoft XPS Document Writer"
        doc.PrinterSettings.PrintToFile = True
        doc.PrinterSettings.PrintFileName = textBox4.Text + ".xps"
        doc.Print()

        _fontFamily.Dispose()
        _fontFamily = Nothing
    End Sub

    ' Handler for PrintPageEvents
    Sub PrintPage(ByVal o As Object, ByVal e As PrintPageEventArgs)
        Dim p As New Point(100, 100)
        Dim sb As New StringBuilder()

        sb.Append("*")
        sb.Append(textBox4.Text)
        sb.Append("*")

        Dim font As New Font(_fontFamily, Int32.Parse(textBox5.Text), FontStyle.Regular, GraphicsUnit.Point)
        e.Graphics.DrawString(sb.ToString(), font, New SolidBrush(Color.Black), p.X, p.Y)
        Dim textSize As SizeF = e.Graphics.MeasureString(sb.ToString(), font)
        font.Dispose()

        font = New Font("Arial", 12, FontStyle.Regular, GraphicsUnit.Point)
        e.Graphics.DrawString(textBox4.Text, font, New SolidBrush(Color.Black), p.X + 10, p.Y + textSize.Height)
        font.Dispose()

    End Sub

    Private Sub textBox4_TextChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles textBox4.TextChanged
        If String.IsNullOrEmpty(textBox4.Text) Then
            Return
        End If

        generateBarcodeImage()
    End Sub

    Private Sub buttonReadFingers_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles buttonReadFingers.Click
        buttonLeftHand.Enabled = False
        buttonRightHand.Enabled = False
        buttonThumbs.Enabled = False

        'TextBoxID.Enabled = True

        Dim id As Integer

        If (TextBoxID.Text.Length = 0 OrElse Not Int32.TryParse(TextBoxID.Text, id)) Then
            toolStripStatusLabelError.Text = "Please enter a valid ID"
            toolStripStatusLabelError.ForeColor = Color.Red
            Exit Sub
        Else
            toolStripStatusLabelError.Text = ""
        End If

        tabControl1.SelectTab("tabPage3")
        Application.DoEvents()

        'Dim fingersCollection As ArrayList
        Dim buff As Byte() = Nothing
        Try
            Dim db As New DBUtil()
            buff = db.GetImage(id, True)

            If buff Is Nothing OrElse buff.Length = 0 Then
                toolStripStatusLabelError.Text = "Can't read fingers' images using ID provided"
                toolStripStatusLabelError.ForeColor = Color.Red
                Exit Sub
            End If

            Dim ms As IO.MemoryStream = Nothing

            ms = New MemoryStream(buff)
            ' Construct a BinaryFormatter and use it to deserialize the data to the stream.
            Dim formatter As New BinaryFormatter
            '_fingersCollection = New ArrayList(10)

            Try
                formatter.Binder = New GenericBinder(Of WsqImage)
                _fingersCollection = formatter.Deserialize(ms)
            Catch ex As SerializationException
                toolStripStatusLabelError.ForeColor = Color.Red
                toolStripStatusLabelError.Text = ex.Message
            Finally
                ms.Close()
            End Try

            Dim i As Integer
            Dim pb As PictureBox

            Dim lock As New Object

            For i = 0 To 9
                pb = Me.Controls.Find("fpPictureBox" + If(i + 1 < 9, (i + 1).ToString(), (i + 2).ToString()), True)(0)
                If _fingersCollection(i) IsNot Nothing Then
                    Dim wsq As WsqImage
                    wsq = _fingersCollection(i)

                    SyncLock lock
                        buff = _fps.ConvertWSQToBmp(wsq)
                        _fps.DisposeWSQImage()
                    End SyncLock

                    If buff IsNot Nothing Then
                        If buff.Length = 1 Then
                            Continue For
                        End If
                    End If

                    'pb = Me.Controls.Find("fpPictureBox" + If(i + 1 < 9, (i + 1).ToString(), (i + 2).ToString()), True)(0)
                    If buff Is Nothing Then
                        pb.Image = Nothing
                    Else
                        ms = New MemoryStream(buff)
                        pb.Image = Image.FromStream(ms)
                        pb.SizeMode = PictureBoxSizeMode.Zoom
                    End If
                Else
                    'pb = Me.Controls.Find("fpPictureBox" + If(i + 1 < 9, (i + 1).ToString(), (i + 2).ToString()), True)(0)
                    pb.Image = Nothing
                End If
            Next

        Catch ex As Exception
            toolStripStatusLabelError.ForeColor = Color.Red
            toolStripStatusLabelError.Text = ex.Message
        Finally
            buttonLeftHand.Enabled = True
            buttonRightHand.Enabled = True
            buttonThumbs.Enabled = True

            TextBoxID.Focus()
        End Try

    End Sub

    Private Sub fpPictureBox_MouseHover(sender As Object, e As System.EventArgs)
        Dim pb As PictureBox = DirectCast(sender, PictureBox)
        If pb.Image IsNot Nothing Then
            Dim ratio = pb.PreferredSize.Height / pb.PreferredSize.Width
            PictureBox4.Image = pb.Image
            PictureBox4.SizeMode = PictureBoxSizeMode.Zoom
            Panel1.Width = pb.PreferredSize.Width / 1.5
            Panel1.Height = pb.PreferredSize.Width / 1.5 * ratio
            If pb.Name = "fpPictureBox4" Or pb.Name = "fpPictureBox5" Then
                Panel1.Location = New Point(TabPage3.Width - Panel1.Width, 0)
            Else
                Panel1.Location = New Point(0, 0)
            End If
            'PictureBox4.BringToFront()
            Panel1.Visible = True
        End If
    End Sub

    Private Sub fpPictureBox_MouseLeave(sender As System.Object, e As System.EventArgs)
        Dim pb As PictureBox = DirectCast(sender, PictureBox)
        'PictureBox4.SendToBack()
        'PictureBox4.Width = 1

        PictureBox4.Image = Nothing
        Panel1.Visible = False
    End Sub
End Class

