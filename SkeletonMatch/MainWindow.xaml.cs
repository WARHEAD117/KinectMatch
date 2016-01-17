//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonRecord
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Windows.Threading;
    using Microsoft.Win32;
    using System.Windows.Input;
    using System.Text.RegularExpressions;
    using System.Text;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = new SolidColorBrush(Color.FromArgb(255, 150, 150, 150));

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing group for picture
        /// </summary>
        private DrawingGroup drawingPicGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;
        private DrawingImage picShower;
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        private bool m_bIsRecording = false;

        DispatcherTimer timer = new DispatcherTimer();

        private int m_deltaTime = 0;
        private int m_timer = 0;
        private int m_startTime = 0;

        private int m_unsavedTimer = 0;

        List<Preview> m_PreviewList;

        class SkelData
        {
            public Tuple<float, float, float, JointType>[] jointList = new Tuple<float, float, float, JointType>[25];
            public int timeMark;

            public SkelData()
            {
                for (int i = 0; i < jointList.Length; i++ )
                {
                    jointList[i] = new Tuple<float, float, float, JointType>(0, 0, 0, JointType.SpineBase);
                }
                timeMark = 0;
            }
        }

        private List<SkelData> m_skelDatas = new List<SkelData>();


        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader colorFrameReader = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap colorBitmap = null;


        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        //==========================================================================================================================
        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        public ImageSource ShowPic
        {
            get
            {
                return this.picShower;
            }
        }
        //========================================================================================================
        bool isShowMode = false;

        public int m_kinectDrawWidth = 118;
        public int m_kinectDrawHeight = 136;

        public int m_showHeight = 768;
        public int m_showWidth = 432;

        int iconSize = (int)(230.0f * (1.0f / 1080 * 432));

        int infoWidth = (int)(1080.0f * (1.0f / 1080 * 432));
        int infoHeight = (int)(552.0f * (1.0f / 1080 * 432));

        int titleWidth = (int)(962.0f * (1.0f / 1080 * 432));
        int titleHeight = (int)(122.0f * (1.0f / 1080 * 432));

        bool m_hasChecked = false;

        int m_checkDelay = 23330;
        int m_maxDelay = 5000;
        int m_drawFlag = -1;

        float m_speed = 1000;

        int m_lastCheckedPic = -1;

        bool hasInitImage = false;

        float previewOffset = 17;

        Point detectCenter = new Point(0, 1.5f);
        float detectRadius = 0.5f;

        List<BitmapImage> InfoImageList;
        List<BitmapImage> ImageList;
        List<BitmapImage> MoveImageList;

        private float colorFrameOffset = -75.0f;

        private int flag = -1;


        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();


            //===================================================================

            // open the reader for the color frames
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();

            // wire handler for frame arrival
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;

            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            // create the bitmap to display
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            //=====================================================================================================================
            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            this.drawingPicGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            this.picShower = new DrawingImage(this.drawingPicGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            timer = new DispatcherTimer(DispatcherPriority.Normal);
            timer.Tick += new EventHandler(timer_Tick);
            timer.Interval = TimeSpan.FromSeconds(0.001);   //设置刷新的间隔时间
            timer.Start();

            m_bIsRecording = false;
            m_timer = 0;
            m_startTime = Environment.TickCount;
            m_unsavedTimer = 0;

            int previewNum = 14;
            m_PreviewList = new List<Preview>();
            for(int i = 0; i < 5; i++)
            {
                for(int j = 0; j < 3; j++)
                {
                    int x = 0;
                    int y = 0;
                    if (i == 0 && j == 0)
                        continue;

                    x = (int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(j * 360.0f * (1.0f / 1080 * m_showWidth));
                    y = (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(i * 342.0f * (1.0f / 1920 * m_showHeight));
                    
                    Preview preIcon = new Preview(x, y);

                    m_PreviewList.Add(preIcon);
                }
            }

            m_PreviewList[0] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(1 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(0 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/01/");
            m_PreviewList[1] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(2 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(0 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/02/");
            m_PreviewList[2] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(0 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(1 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/03/");
            m_PreviewList[3] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(1 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(1 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/04/");
            m_PreviewList[4] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(2 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(1 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/05/");
            m_PreviewList[5] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(0 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(2 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/06/");
            m_PreviewList[6] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(1 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(2 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/07/");
            m_PreviewList[7] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(2 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(2 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/08/");
            m_PreviewList[8] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(0 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(3 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/09/");
            m_PreviewList[9] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(1 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(3 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/10/");
            m_PreviewList[10] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(2 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(3 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/11/");
            m_PreviewList[11] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(0 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(4 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/12/");
            m_PreviewList[12] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(1 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(4 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/13/");
            m_PreviewList[13] = new Preview((int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(2 * 360.0f * (1.0f / 1080 * m_showWidth)),
                (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(4 * 342.0f * (1.0f / 1920 * m_showHeight)), "/AnimMove/14/");
            


            bool result = LoadPic("/Tex/", out ImageList);
            result = LoadPic("/InfoTex/", out InfoImageList);
            result = LoadPic("/MoveTex/", out MoveImageList);

            LoadConfig();
        }



        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        private bool LoadPic()
        {
            if (ImageList == null)
            {
                ImageList = new List<BitmapImage>();
            }

            String imgFolder = System.IO.Path.GetDirectoryName(Application.ResourceAssembly.Location) + @"/Tex/";
            if (!Directory.Exists(imgFolder))
                return false;

            DirectoryInfo ImgFolderInfo = new DirectoryInfo(imgFolder);


            List<String> fileList = new List<String>();
            foreach (FileInfo nextFile in ImgFolderInfo.GetFiles())
            {
                fileList.Add(nextFile.FullName);
            }

            int imgCount = fileList.Count;
            ImageList.Clear();

            for (int i = 0; i < imgCount; i++)
            {
                String imgPath = fileList[i];
                bool isExist = File.Exists(imgPath.ToString());
                if (isExist)
                {
                    BitmapImage newImage = new BitmapImage(new Uri(imgPath, UriKind.Absolute));
                    ImageList.Add(newImage);
                }

            }
            return true;
        }




        private bool LoadPic(string folderName, out List<BitmapImage> texList)
        {
            texList = new List<BitmapImage>();

            String imgFolder = System.IO.Path.GetDirectoryName(Application.ResourceAssembly.Location) + folderName;
            if (!Directory.Exists(imgFolder))
                return false;

            DirectoryInfo ImgFolderInfo = new DirectoryInfo(imgFolder);


            List<String> fileList = new List<String>();
            foreach (FileInfo nextFile in ImgFolderInfo.GetFiles())
            {
                fileList.Add(nextFile.FullName);
            }

            int imgCount = fileList.Count;
            texList.Clear();

            for (int i = 0; i < imgCount; i++)
            {
                String imgPath = fileList[i];
                bool isExist = File.Exists(imgPath.ToString());
                if (isExist)
                {
                    BitmapImage newImage = new BitmapImage(new Uri(imgPath, UriKind.Absolute));
                    texList.Add(newImage);
                }

            }
            return true;
        }


        void LoadConfig()
        {
            String configPath = System.IO.Path.GetDirectoryName(Application.ResourceAssembly.Location) + @"/config.txt";
            if (File.Exists(configPath))
            {
                StreamReader sr = new StreamReader(configPath, Encoding.Default);

                string detectCenterX_S = null;
                string detectCenterY_S = null;
                string detectRadius_S = null;

                int tempNum = -1;

                if (sr.Peek() > 0)
                {
                    detectCenterX_S = sr.ReadLine();
                    if (detectCenterX_S != "" || detectCenterX_S != null)
                    {
                        tempNum = GetNum(detectCenterX_S, "DetectCenterX");
                        if (tempNum != -1)
                        {
                            detectCenter.X = tempNum / 100.0f;
                        }
                    }
                }
                if (sr.Peek() > 0)
                {
                    detectCenterY_S = sr.ReadLine();
                    if (detectCenterY_S != "" || detectCenterY_S != null)
                    {
                        tempNum = GetNum(detectCenterY_S, "DetectCenterY");
                        if (tempNum != -1)
                        {
                            detectCenter.Y = tempNum / 100.0f;
                        }
                    }
                }
                if (sr.Peek() > 0)
                {
                    detectRadius_S = sr.ReadLine();
                    if (detectRadius_S != "" || detectRadius_S != null)
                    {
                        tempNum = GetNum(detectRadius_S, "DetectRadius");
                        if (tempNum != -1)
                        {
                            detectRadius = tempNum / 100.0f;
                        }
                    }
                }
                sr.Close();

            }
        }

        private int GetNum(string str, string markStr)
        {
            int num = -1;

            string pattern = markStr + @":[0-9]*";
            string result = Regex.Match(str, pattern).Value;
            string patternGetNum = @"[0-9]\d*";
            string num_result = Regex.Match(result, patternGetNum).Value;

            num = System.Int32.Parse(num_result);
            return num;
        }


        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, 160/*this.displayWidth*/, 240/*this.displayHeight*/));

                    dc.DrawImage(colorBitmap, new Rect(colorFrameOffset, 0, (int)(m_kinectDrawHeight * (16.0f / 9.0f)), m_kinectDrawHeight));
                    int penIndex = 0;
                    foreach (Body body in this.bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];
                        SolidColorBrush bodyBrush= new SolidColorBrush(Color.FromArgb(255, 104, 25, 15));
                        drawPen = new Pen(bodyBrush, 6);

                        if (body.IsTracked)
                        {
                            //this.DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                            //去除不在范围内的人
                            Point basePoint = new Point(joints[JointType.SpineBase].Position.X, joints[JointType.SpineBase].Position.Z);
                            double dis_pow2 = Math.Pow((basePoint.X - detectCenter.X), 2.0f) + Math.Pow((basePoint.Y - detectCenter.Y), 2.0f);
                            if (dis_pow2 > detectRadius * detectRadius)
                            {
                                continue;
                            }

                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                //jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                                jointPoints[jointType] = new Point(colorFrameOffset/2.7f + depthSpacePoint.X / this.displayHeight * m_kinectDrawHeight, depthSpacePoint.Y / this.displayHeight * m_kinectDrawHeight);
                            }

                            //记录骨骼
                            //if(m_bIsRecording)
                                //RecordSkelton(joints);

                            flag = Judge(joints, jointPoints);

                            
                            this.DrawBody(joints, jointPoints, dc, drawPen);

                            //this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            //this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);
                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, m_kinectDrawWidth/*this.displayWidth*/, m_kinectDrawHeight/*this.displayHeight*/));
                }
            }

            
//             {
//                 using (DrawingContext dc = this.drawingPicGroup.Open())
//                 {
//                     DrawShow(dc, flag);
//                     
//                 }
//             }

        }

        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        this.colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(
                                this.colorBitmap.BackBuffer,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                        }

                        this.colorBitmap.Unlock();
                    }
                }
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }


        void timer_Tick(object sender, EventArgs e)
        {
            int tempTimer = m_timer;
            m_timer = Environment.TickCount - m_startTime;
            m_deltaTime = m_timer - tempTimer;

            m_checkDelay += m_deltaTime;

            UpdateInfo();

            //this.Title = string.Concat("TimerWindow  ", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));
            SelfController();
        }

        private void UpdateInfo()
        {
            using (DrawingContext dc = this.drawingPicGroup.Open())
            {
                DrawShow(dc, flag);

            }
        }

        void SelfController()
        {
            //按1进入showmode, 按2恢复
            if (Keyboard.IsKeyDown(Key.D1))
            {
                if (!isShowMode)
                {
                    isShowMode = true;
                    SetShowMode();
                }
            }
            if (Keyboard.IsKeyDown(Key.D2))
            {
                if (isShowMode)
                {
                    isShowMode = false;
                    SetDebugMode();
                }
            }

        }

        void SetShowMode()
        {
            //---------------------------------------------------------
            // 去掉边框放置在左上角   
            this.Left = -10.0;
            this.Top = 0.0;
            this.Width = this.m_showWidth + 20;
            this.Height = this.m_showHeight;

            this.WindowState = System.Windows.WindowState.Normal;
            this.WindowStyle = System.Windows.WindowStyle.None;
            this.ResizeMode = System.Windows.ResizeMode.NoResize;
            this.Topmost = true;
        }

        void SetDebugMode()
        {
            //---------------------------------------------------------
            // 去掉边框放置在左上角   
            this.Left = 0.0;
            this.Top = 0.0;
            this.Width = this.m_showWidth + 50;
            this.Height = this.m_showHeight + 50;

            this.WindowState = System.Windows.WindowState.Normal;
            this.WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
            this.ResizeMode = System.Windows.ResizeMode.CanResize;
            this.Topmost = false;
        }



        void DrawShow(DrawingContext dc, int flag)
        {
            dc.DrawRectangle(Brushes.Black, null, new Rect(-100.0, -100.0, 1, 1));
            // Draw a transparent background to set the render size
            SolidColorBrush bgBrush= new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
            dc.DrawRectangle(bgBrush, null, new Rect(0.0, 0.0, m_showWidth, m_showHeight));

            if (m_checkDelay > m_maxDelay && flag < 0)
            {
                m_hasChecked = false;
            }

            if(!hasInitImage)
            {
                for (int i = 0; i < ImageList.Count; i++)
                {
                    dc.DrawImage(ImageList[i], new Rect(0, 0, m_showWidth, m_showHeight));
                }
                hasInitImage = true;
            }

            if (ImageList.Count > 0)
            {

                if (flag >= 0 && ImageList.Count > flag && m_checkDelay > m_maxDelay)
                {
                    //dc.DrawRectangle(Brushes.BlueViolet, null, new Rect(0.0, 0.0, 420, 500));

                    m_drawFlag = flag;
                    m_hasChecked = true;
                    m_checkDelay = 0;
                }

                if (m_hasChecked && m_drawFlag >= 0 && m_drawFlag < ImageList.Count)
                {
                    m_lastCheckedPic = m_drawFlag;
                    dc.DrawImage(ImageList[m_drawFlag], new Rect(0, 0, m_showWidth, m_showHeight));
                }
            }

            //Geometry ellipse = new EllipseGeometry(new Point(200, 70), 100, 50);
            //GeometryDrawing drawing = new GeometryDrawing(Brushes.LightBlue, new Pen(Brushes.Green, 1), ellipse);
            //dc.DrawDrawing(drawing);
            
            //dc.DrawEllipse(Brushes.Red, null, new Point(m_showWidth - 50, 0), 50, 50);

            

            if(m_hasChecked == false)
            {
                for (int i = 0; i < m_PreviewList.Count; i++)
                {
                    m_PreviewList[i].ResetPos();
                }
                if (m_lastCheckedPic < ImageList.Count && m_lastCheckedPic >= 0)
                    dc.DrawImage(ImageList[m_lastCheckedPic], new Rect(0, 0, m_showWidth, m_showHeight));
                dc.DrawRectangle(bgBrush, null, new Rect(0.0, 0.0, m_showWidth, m_showHeight));

                int titleX = (int)(65.0f * (1.0f / 1080 * m_showWidth)) + (int)(0 * 360.0f * (1.0f / 1080 * m_showWidth));
                int titleY = (int)(56.0f * (1.0f / 1920 * m_showHeight)) + (int)(5 * 342.0f * (1.0f / 1920 * m_showHeight));
                dc.DrawImage(InfoImageList[InfoImageList.Count - 1], new Rect(titleX, titleY, titleWidth, titleHeight));
            }


            if (m_hasChecked && m_drawFlag < InfoImageList.Count)
            {
                dc.DrawImage(InfoImageList[m_drawFlag], new Rect(0, m_showHeight - infoHeight, infoWidth, infoHeight));
            }

            SolidColorBrush previewBrush = new SolidColorBrush(Color.FromArgb(178, 0, 0, 0));
            for (int i = 0; i < m_PreviewList.Count; i++)
            {
                if (i != m_drawFlag && m_hasChecked == true)
                    continue;

                float x = m_PreviewList[i].PosX;
                float y = m_PreviewList[i].PosY;
                if (!m_hasChecked)
                {
                    x = m_PreviewList[i].PosX;
                    y = m_PreviewList[i].PosY;
                }
                else
                {
                    x = m_PreviewList[i].PosX;
                    if (m_PreviewList[i].PosY < m_showHeight - infoHeight + previewOffset)
                    {
                        float speed = m_speed;
                        float changePos = (m_showHeight - infoHeight + previewOffset) * 0.7f;
                        if (m_PreviewList[i].PosY > changePos)
                        {
                            float a = 1.0f * (m_showHeight - m_PreviewList[i].PosY) / (m_showHeight - changePos);
                            speed = a * a * a * m_speed;
                        }
                        m_PreviewList[i].PosY += m_deltaTime / 1000.0f * speed;
                        
                        
                    }
                    if (m_PreviewList[i].PosY > m_showHeight - infoHeight + previewOffset)
                    {
                        m_PreviewList[i].PosY = m_showHeight - infoHeight + previewOffset;
                    }
                    y = m_PreviewList[i].PosY;
                }


                //dc.DrawRectangle(previewBrush, null, new Rect(x, y, iconSize,iconSize));
                //dc.DrawImage(MoveImageList[i], new Rect(x, y, iconSize, iconSize));
                //if(i == 1)
                {
                    m_PreviewList[i].DrawAnimPic(dc, m_deltaTime, x, y, iconSize, iconSize);
                }
            }
        }

        private int Judge(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints)
        {
            //坐标系：
            //0 ----------> +x
            //  |
            //  |
            //  |
            //  |
            //  v +y
            //
            //头到腰的连线的角度
            double deltaHeadToHip_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHeadToHip_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double degreeHeadToHip = Math.Abs(Math.Atan(deltaHeadToHip_Y / deltaHeadToHip_X));

            //左手到肩膀的连线
            double deltaLHandToShoulder_X = jointPoints[JointType.HandLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLHandToShoulder_Y = jointPoints[JointType.HandLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double degreeLHandToShoulder = Math.Abs(Math.Atan(deltaLHandToShoulder_Y / deltaLHandToShoulder_X));

            if (degreeHeadToHip >= Math.PI * 0.166 && degreeHeadToHip <= Math.PI * 0.33)
            {
                if (deltaHeadToHip_X < 0)
                {
                    if(degreeLHandToShoulder >= Math.PI * 0.25 && degreeLHandToShoulder <= Math.PI * 0.33)
                    {
                        if (deltaLHandToShoulder_X < 0)
                        {
                            if(jointPoints[JointType.HandRight].X < jointPoints[JointType.ElbowRight].X
                            && jointPoints[JointType.HandRight].Y > jointPoints[JointType.ElbowRight].Y)
                            {
                                return 0;
                            }
                        }
                        
                        
                    }

                    if (degreeLHandToShoulder <= Math.PI * 0.5 && degreeLHandToShoulder >= Math.PI * 0.33)
                    {
                        if (jointPoints[JointType.HandRight].X < jointPoints[JointType.ElbowRight].X
                            && jointPoints[JointType.HandRight].Y < jointPoints[JointType.ElbowRight].Y)
                        {
                            return 1;
                        }

                    }
                }
            }

            //
            

            //左肘到左肩的连线
            double deltaLElbowToShoulder_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLElbowToShoulder_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double degreeLElbowToShoulder = Math.Abs(Math.Atan(deltaLElbowToShoulder_Y / deltaLElbowToShoulder_X));

            //右肘到右肩的连线
            double deltaRElbowToShoulder_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRElbowToShoulder_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double degreeRElbowToShoulder = Math.Abs(Math.Atan(deltaRElbowToShoulder_Y / deltaRElbowToShoulder_X));

            //左手到左肘的连线
            double deltaLHandToElbow_X = jointPoints[JointType.HandLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLHandToElbow_Y = jointPoints[JointType.HandLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double degreeLHandToElbow = Math.Abs(Math.Atan(deltaLHandToElbow_Y / deltaLHandToElbow_X));

            //右手到右肘的连线
            double deltaRHandToElbow_X = jointPoints[JointType.HandRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRHandToElbow_Y = jointPoints[JointType.HandRight].Y - jointPoints[JointType.ElbowRight].Y;
            double degreeRHandToElbow = Math.Abs(Math.Atan(deltaRHandToElbow_Y / deltaRHandToElbow_X));

            if (degreeHeadToHip >= Math.PI * 0.38 && degreeHeadToHip <= Math.PI * 0.45)
            {
                if (deltaHeadToHip_X > 0)
                {
                    bool isTrueLeft = false;
                    if (degreeLElbowToShoulder > Math.PI * 0.25 && degreeLElbowToShoulder < Math.PI * 0.4)
                    {
                        if (deltaLElbowToShoulder_X < 0 && deltaLElbowToShoulder_Y > 0)
                        {
                            if (degreeLHandToElbow < Math.PI * 0.25 && deltaLHandToElbow_X > 0)
                            {
                                isTrueLeft = true;
                            }
                        }
                    }
                    bool isTrueRight = false;
                    if (degreeRElbowToShoulder < Math.PI * 0.38)
                    {
                        if (deltaRElbowToShoulder_X > 0 && deltaRElbowToShoulder_Y > 0)
                        {
                            if (degreeRHandToElbow < Math.PI * 0.33 && deltaRHandToElbow_X < 0)
                            {
                                isTrueRight = true;
                            }
                        }
                    }

                    if (isTrueLeft && isTrueRight)
                    {
                        return 2;
                    }
                }
            }

            //有点难
            if (degreeHeadToHip >= Math.PI * 0.2 && degreeHeadToHip <= Math.PI * 0.45)
            {
                if (deltaHeadToHip_X > 0)
                {
                    bool isTrueLeft = false;
                    if (degreeLElbowToShoulder > Math.PI * 0.1 && degreeLElbowToShoulder < Math.PI * 0.4)
                    {
                        if (deltaLElbowToShoulder_X < 0 && deltaLElbowToShoulder_Y > 0)
                        {
                            if (degreeLHandToElbow < Math.PI * 0.25 && deltaLHandToElbow_X > 0)
                            {
                                isTrueLeft = true;
                            }
                        }
                    }
                    bool isTrueRight = false;
                    if (degreeRElbowToShoulder < Math.PI * 0.4)
                    {
                        if (deltaRElbowToShoulder_X < 0 && deltaRElbowToShoulder_Y > 0)
                        {
                            if (degreeRHandToElbow > Math.PI * 0.35 && deltaRHandToElbow_Y < 0)
                            {
                                isTrueRight = true;
                            }
                        }
                    }

                    if(isTrueLeft && isTrueRight)
                    {
                        return 3;
                    }
                }
                
            }

            //有点难
            if (degreeHeadToHip >= Math.PI * 0.35 && degreeHeadToHip <= Math.PI * 0.48)
            {
                if (deltaHeadToHip_X < 0)
                {
                    if (degreeLElbowToShoulder > Math.PI * 0.35 && deltaLElbowToShoulder_Y > 0)
                    {
                        if (degreeLHandToElbow > Math.PI * 0.38 && deltaLHandToElbow_Y < 0)
                        {
                            if (degreeRElbowToShoulder < Math.PI * 0.4 && degreeRElbowToShoulder > Math.PI * 0.1 && deltaRElbowToShoulder_Y > 0)
                            {
                                if (deltaRHandToElbow_X < 0 && deltaRHandToElbow_Y > 0)
                                {
                                    if (degreeRHandToElbow < Math.PI * 0.45 && degreeRHandToElbow > Math.PI * 0.2)
                                    {
                                        return 4;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (degreeLElbowToShoulder >= 0.35 * Math.PI && degreeRElbowToShoulder >= 0.35 * Math.PI)
            {
                if(deltaLHandToElbow_X < 0 && deltaRHandToElbow_X < 0)
                {
                    if (deltaLHandToElbow_Y < 0 && deltaRHandToElbow_Y < 0)
                    {
                        //return 5;
                    }
                }
            }

            
            //头到脖子的连线的角度
            double deltaHeadToShoulderCenter_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaHeadToShoulderCenter_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double degreeHeadToShoulderCenter = Math.Abs(Math.Atan(deltaHeadToShoulderCenter_Y / deltaHeadToShoulderCenter_X));

            if (deltaHeadToShoulderCenter_X > 0 && degreeHeadToShoulderCenter < 0.47 * Math.PI)
            {
                if (degreeLElbowToShoulder >= 0.35 * Math.PI && degreeRElbowToShoulder <= 0.25 * Math.PI)
                {
                    if (deltaLHandToElbow_Y < 0 && deltaRHandToElbow_Y < 0)
                    {
                        if (degreeLHandToElbow > 0.35 * Math.PI && degreeRHandToElbow > 0.4 * Math.PI)
                        {
                            return 6;
                        }
                    }
                }
            }

            

            if (degreeHeadToHip < 0.46 * Math.PI)
            {
                if(deltaLElbowToShoulder_Y > 0 && deltaRElbowToShoulder_Y > 0)
                {
                    if (degreeLElbowToShoulder >= 0.35 * Math.PI && degreeRElbowToShoulder >= 0.2 * Math.PI)
                    {
                        if (deltaLHandToElbow_X < 0 && deltaRHandToElbow_X < 0)
                        {
                            if (deltaLHandToElbow_Y < 0 && deltaRHandToElbow_Y < 0)
                            {
                                if (degreeLHandToElbow < 0.4 * Math.PI && degreeRHandToElbow < 0.3 * Math.PI)
                                {
                                    return 7;
                                }
                            }
                        }
                    }
                }
            }


            if (degreeHeadToHip < 0.46 * Math.PI)
            {
                if (deltaLElbowToShoulder_Y > 0 && deltaRElbowToShoulder_Y > 0)
                {
                    if (degreeLElbowToShoulder >= 0.35 * Math.PI && degreeRElbowToShoulder >= 0.2 * Math.PI)
                    {
                        if (deltaLHandToElbow_X > 0 && deltaRHandToElbow_X < 0)
                        {
                            if (deltaLHandToElbow_Y < 0 && deltaRHandToElbow_Y < 0)
                            {
                                if (degreeLHandToElbow < 0.4 * Math.PI && degreeRHandToElbow < 0.4 * Math.PI)
                                {
                                    return 8;
                                }
                            }
                        }
                    }
                }
                
            }


            if (degreeHeadToHip > 0.4 * Math.PI)
            {
                if (deltaLElbowToShoulder_Y > 0 && deltaRElbowToShoulder_Y > 0)
                {
                    if (degreeRElbowToShoulder > Math.PI * 0.3 && deltaLElbowToShoulder_Y > 0)
                    {
                        if (degreeLHandToElbow > Math.PI * 0.2 && deltaLHandToElbow_Y > 0 && deltaLHandToElbow_X > 0)
                        {
                            if (degreeRElbowToShoulder > Math.PI * 0.25)
                            {
                                if (deltaRHandToElbow_X < 0 && deltaRHandToElbow_Y < 0)
                                {
                                    if (degreeRHandToElbow < Math.PI * 0.45 && degreeRHandToElbow > Math.PI * 0.2)
                                    {
                                        return 9;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (degreeLElbowToShoulder >= 0.35 * Math.PI && degreeRElbowToShoulder >= 0.35 * Math.PI)
            {
                if (deltaLElbowToShoulder_Y > 0 && deltaRElbowToShoulder_Y > 0)
                {
                    if (deltaLHandToElbow_X > 0 && deltaRHandToElbow_X > 0)
                    {
                        if (deltaLHandToElbow_Y < 0 && deltaRHandToElbow_Y < 0)
                        {
                            return 10;
                        }
                    }
                }
                
            }

            if (degreeHeadToHip < 0.46 * Math.PI && deltaHeadToHip_X < 0)
            {
                if (deltaLElbowToShoulder_Y > 0 && deltaRElbowToShoulder_Y > 0)
                {
                    if (degreeLElbowToShoulder >= 0.35 * Math.PI && degreeRElbowToShoulder >= 0.2 * Math.PI)
                    {
                        if (deltaLHandToElbow_X < 0 && deltaRHandToElbow_X < 0)
                        {
                            if (deltaLHandToElbow_Y < 0 && deltaRHandToElbow_Y > 0)
                            {
                                if (degreeLHandToElbow < 0.4 * Math.PI && degreeLHandToElbow > 0.2 * Math.PI)
                                {
                                    if (degreeRHandToElbow < 0.45 * Math.PI && degreeRHandToElbow > 0.2 * Math.PI)
                                    {
                                        return 11;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (degreeLElbowToShoulder >= 0.35 * Math.PI && degreeRElbowToShoulder >= 0.2 * Math.PI)
            {
                if (deltaLElbowToShoulder_Y > 0 && deltaRElbowToShoulder_Y > 0)
                {
                    if (deltaLHandToElbow_X < 0 && deltaRHandToElbow_X < 0)
                    {
                        if (deltaLHandToElbow_Y > 0 && deltaRHandToElbow_Y < 0)
                        {
                            if (degreeLHandToElbow < 0.3 * Math.PI && degreeLHandToElbow > 0.06 * Math.PI)
                            {
                                if (degreeRHandToElbow < 0.45 * Math.PI && degreeRHandToElbow > 0.2 * Math.PI)
                                {
                                    return 12;
                                }
                            }
                        }
                    }
                }
            }

            if (deltaHeadToHip_X < 0)
            {
                if (deltaLElbowToShoulder_Y > 0 && deltaRElbowToShoulder_Y < 0)
                {
                    if (degreeLElbowToShoulder >= 0.35 * Math.PI && degreeRElbowToShoulder <= 0.35 * Math.PI)
                    {
                        if (deltaRHandToElbow_X < 0 && deltaRHandToElbow_Y < 0)
                        {
                            if (degreeRHandToElbow > 0.25 * Math.PI && degreeRHandToElbow < 0.4 * Math.PI)
                            {
                                if (deltaLHandToElbow_Y < 0)
                                {
                                    if (degreeLHandToElbow > 0.35 * Math.PI)
                                    {
                                        return 13;
                                    }
                                }
                            }
                        }
                    }
                }
            }

           // if (jointPoints[JointType.HandRight].Y < jointPoints[JointType.Head].Y)
           //     return 2;

           // if (jointPoints[JointType.HandLeft].Y < jointPoints[JointType.Head].Y)
           //     return 3;

            if (jointPoints[JointType.HandLeft].Y > jointPoints[JointType.HandRight].Y
                && jointPoints[JointType.HandRight].Y < jointPoints[JointType.Head].Y
                //&&
                //jointPoints[JointType.ElbowLeft].Y > jointPoints[JointType.ElbowRight].Y &&
                //jointPoints[JointType.ElbowLeft].Y > jointPoints[JointType.Head].Y &&
                //jointPoints[JointType.ElbowRight].Y > jointPoints[JointType.Head].Y &&
                //jointPoints[JointType.HandLeft].Y > jointPoints[JointType.Head].Y &&
                //jointPoints[JointType.HandRight].Y > jointPoints[JointType.Head].Y &&
                //jointPoints[JointType.HandLeft].Y < jointPoints[JointType.SpineBase].Y &&
                //jointPoints[JointType.HandRight].Y < jointPoints[JointType.SpineBase].Y &&
                //jointPoints[JointType.SpineShoulder].X > jointPoints[JointType.SpineMid].X &&
                //jointPoints[JointType.Head].X > jointPoints[JointType.SpineShoulder].X &&
                //jointPoints[JointType.HandLeft].Y - jointPoints[JointType.SpineShoulder].X < 20 &&
                //(joints[JointType.KneeLeft].TrackingState == TrackingState.Inferred || joints[JointType.KneeRight].TrackingState == TrackingState.Inferred)
                )
                return -10;
            if (jointPoints[JointType.HandLeft].Y < jointPoints[JointType.HandRight].Y
                && jointPoints[JointType.HandLeft].Y < jointPoints[JointType.Head].Y
                //&&
                //jointPoints[JointType.ElbowLeft].Y > jointPoints[JointType.ElbowRight].Y &&
                //jointPoints[JointType.ElbowLeft].Y > jointPoints[JointType.Head].Y &&
                //jointPoints[JointType.ElbowRight].Y > jointPoints[JointType.Head].Y &&
                //jointPoints[JointType.HandLeft].Y > jointPoints[JointType.Head].Y &&
                //jointPoints[JointType.HandRight].Y > jointPoints[JointType.Head].Y &&
                //jointPoints[JointType.HandLeft].Y < jointPoints[JointType.SpineBase].Y &&
                //jointPoints[JointType.HandRight].Y < jointPoints[JointType.SpineBase].Y &&
                //jointPoints[JointType.SpineShoulder].X > jointPoints[JointType.SpineMid].X &&
                //jointPoints[JointType.Head].X > jointPoints[JointType.SpineShoulder].X &&
                //jointPoints[JointType.HandLeft].Y - jointPoints[JointType.SpineShoulder].X < 20 &&
                //(joints[JointType.KneeLeft].TrackingState == TrackingState.Inferred || joints[JointType.KneeRight].TrackingState == TrackingState.Inferred)
                )
                return -1;
            return -1;
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    //drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }
        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {
                case HandState.Closed:
                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, m_kinectDrawHeight/*this.displayHeight*/ - ClipBoundsThickness, m_kinectDrawWidth/*this.displayWidth*/, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, m_kinectDrawWidth/*this.displayWidth*/, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, m_kinectDrawHeight/*this.displayHeight*/));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, m_kinectDrawHeight/*this.displayHeight*/));
            }
        }

        
        
    }
}
