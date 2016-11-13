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
    using System.Drawing.Imaging;
    //using 
    //using System.Drawing;

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
        bool isShowMode = true;


        DispatcherTimer timer = new DispatcherTimer();

        private int m_deltaTime = 0;
        private int m_timer = 0;
        private int m_startTime = 0;

        List<Preview> m_PreviewList;

        public int m_showHeight = 768;
        public int m_showWidth = 432;

        public int m_kinectDrawWidth = 160;
        public int m_kinectDrawHeight = 1096;

        private float colorFrameOffsetX = 0;//-420.0f;
        private float colorFrameOffsetY = 200;//200.0f;

        int iconSize = 180;

        int spaceBetweenAnimX = 0;
        int spaceBetweenAnimY = 0;

        int animOffsetX = 240;
        int animOffsetY = 450;

        int infoX = 0;
        int infoY = 1449;
        int infoWidth = 1080;
        int infoHeight = 471;


        int startInfoX = 60;
        int startInfoY = 1047;
        int startInfoWidth = 960;
        int startInfoHeight = 96;

        int titleX = 195;
        int titleY = 125;
        int titleWidth = 691;
        int titleHeight = 251;


        Preview scrollAnim;

        bool m_hasChecked = false;

        bool m_bodyTracked = false;

        bool m_lastBodyTracked = false;


        int m_checkDelay = 23330;
        int m_maxDelay = 9000;
        int m_drawFlag = -1;

        float m_speed = 1000;

        bool hasInitImage = false;


        int previewNum = 14;

        float previewOffset = 17;

        Point detectCenter = new Point(0, 1.5f);
        float detectRadius = 0.5f;

        List<BitmapImage> InfoTexList;
        List<System.Drawing.Image> InfoTexImageList;
        List<BitmapImage> TexList;
        List<System.Drawing.Image> TexImageList;
        List<BitmapImage> MoveAnimList;

        List<BitmapImage> HintList;

        BitmapImage stoneBG;
        BitmapImage startInfoTex;
        BitmapImage titleTex;


        private int flag = -1;

        private int stackCount = 0;
        private int[] moveStack = null;

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
            timer.Interval = TimeSpan.FromSeconds(0.01);   //设置刷新的间隔时间
            timer.Start();

            m_timer = 0;
            m_startTime = Environment.TickCount;



            m_showHeight = 1920;
            m_showWidth = 1080;

            m_kinectDrawWidth = (int)(16.0f / 9.0f * m_kinectDrawHeight);
            colorFrameOffsetX = 1080 / 2 - m_kinectDrawWidth / 2.0f;

            m_kinectDrawWidth = (int)(m_kinectDrawWidth * (1.0f / 1080 * m_showWidth));
            m_kinectDrawHeight = (int)(m_kinectDrawHeight * (1.0f / 1920 * m_showHeight));

            colorFrameOffsetX = (int)(colorFrameOffsetX * (1.0f / 1080 * m_showWidth));
            colorFrameOffsetY = (int)(colorFrameOffsetY * (1.0f / 1920 * m_showHeight));

            iconSize = (int)(iconSize * (1.0f / 1080 * m_showWidth));

            infoX = (int)(infoX * (1.0f / 1080 * m_showWidth));
            infoY = (int)(infoY * (1.0f / 1920 * m_showHeight));
            infoWidth = (int)(infoWidth * (1.0f / 1080 * m_showWidth));
            infoHeight = (int)(infoHeight * (1.0f / 1920 * m_showHeight));

            startInfoX = (int)(startInfoX * (1.0f / 1080 * m_showWidth));
            startInfoY = (int)(startInfoY * (1.0f / 1920 * m_showHeight));
            startInfoWidth = (int)(startInfoWidth * (1.0f / 1080 * m_showWidth));
            startInfoHeight = (int)(startInfoHeight * (1.0f / 1920 * m_showHeight));

            titleX = (int)(titleX * (1.0f / 1080 * m_showWidth));
            titleY = (int)(titleY * (1.0f / 1920 * m_showHeight));
            titleWidth = (int)(titleWidth * (1.0f / 1080 * m_showWidth));
            titleHeight = (int)(titleHeight * (1.0f / 1920 * m_showHeight));

            m_PreviewList = new List<Preview>();

            animOffsetX = (int)(animOffsetX * (1.0f / 1080 * m_showWidth));
            animOffsetY = (int)(animOffsetY * (1.0f / 1920 * m_showHeight));

            spaceBetweenAnimX = (int)(spaceBetweenAnimX * (1.0f / 1080 * m_showWidth));
            spaceBetweenAnimY = (int)(spaceBetweenAnimY * (1.0f / 1920 * m_showHeight));

            int[,] iconList = {{315,498},{576,498},
                                {180,783},{441,783},{702,783},
                                {180,1080},{441,1080},{702,1080},
                                {180,1368},{441,1368},{702,1368},
                                {180,1674},{441,1674},{702,1674}};
           
            int widthCount = 3;
            int heightCount = 5;
            for (int i = 0; i < previewNum; i++ )
            {
                int x = iconList[i,0];
                int y = iconList[i,1];
                x = (int)(x * (1.0f / 1080 * m_showWidth));
                y = (int)(y * (1.0f / 1920 * m_showHeight));
                string numInName = "";
                int numInName_Int = i+1;
                if (numInName_Int < 10)
                    numInName = "0" + numInName_Int.ToString();
                else
                    numInName = numInName_Int.ToString();

                Preview preIcon = new Preview(x, y, "/AnimMove/" + numInName + "/");

                m_PreviewList.Add(preIcon);
            }
            String bgImgPath = System.IO.Path.GetDirectoryName(Application.ResourceAssembly.Location) + @"/AppTex/" + @"stoneBG.png";
            stoneBG = new BitmapImage(new Uri(bgImgPath, UriKind.Absolute));

            String startImgPath = System.IO.Path.GetDirectoryName(Application.ResourceAssembly.Location) + @"/AppTex/" + @"startInfo.jpg";
            startInfoTex = new BitmapImage(new Uri(startImgPath, UriKind.Absolute));

            String titleImgPath = System.IO.Path.GetDirectoryName(Application.ResourceAssembly.Location) + @"/AppTex/" + @"title.png";
            titleTex = new BitmapImage(new Uri(titleImgPath, UriKind.Absolute));

            bool result = LoadPic("/Tex/", out TexList);
            result = LoadPic("/InfoTex/", out InfoTexList);
            result = LoadPic("/MoveTex/", out MoveAnimList);
            result = LoadPic("/HintTex/", out HintList);

            result = LoadPicImage("/Tex/", out TexImageList);

            result = LoadPicImage("/InfoTex/", out InfoTexImageList);

            scrollAnim = new Preview(infoX, infoY, "/InfoAnim/");

            LoadConfig();

            stackCount = 20;
            moveStack = new int[stackCount];

            SetShowMode();
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
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, m_showWidth,m_showHeight));

                    dc.DrawImage(colorBitmap, new Rect(colorFrameOffsetX, colorFrameOffsetY, m_kinectDrawWidth, m_kinectDrawHeight));
                    int penIndex = 0;
                    //找到圈子范围内最靠前的人
                    Body bodyFound = null;
                    float minZ = 100000;
                    foreach (Body body in this.bodies)
                    {
                        if (body.IsTracked)
                        {
                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                            //去除不在范围内的人
                            Point basePoint = new Point(joints[JointType.SpineBase].Position.X, joints[JointType.SpineBase].Position.Z);
                            double dis_pow2 = Math.Pow((basePoint.X - detectCenter.X), 2.0f) + Math.Pow((basePoint.Y - detectCenter.Y), 2.0f);
                            if (dis_pow2 > detectRadius * detectRadius)
                            {
                                continue;
                            }
                            if(joints[JointType.SpineBase].Position.Z < minZ)
                            {
                                bodyFound = body;
                                minZ = joints[JointType.SpineBase].Position.Z;
                            }

                        }
                    }

                    m_lastBodyTracked = m_bodyTracked;
                    m_bodyTracked = false;
                    flag = -1;
                    if (bodyFound != null && bodyFound.IsTracked)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];
                        SolidColorBrush bodyBrush = new SolidColorBrush(Color.FromArgb(255, 78, 204, 51));
                        drawPen = new Pen(bodyBrush, 6);

                        if (bodyFound.IsTracked)
                        {
                            //this.DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = bodyFound.Joints;

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
                                jointPoints[jointType] = new Point(colorFrameOffsetX - 0.45f * colorFrameOffsetX + depthSpacePoint.X / this.displayWidth * m_kinectDrawWidth * 0.85f, colorFrameOffsetY * 0.85f + depthSpacePoint.Y / this.displayHeight * m_kinectDrawHeight * 1.1f);
                            }

                            m_bodyTracked = true;
                            //flag = Judge(joints, jointPoints);

                            //this.DrawBody(joints, jointPoints, dc, drawPen);

                            //this.DrawHand(bodyFound.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            //this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);

                            bool res = false;
                            res = JudgeWithIndex(m_intFlagToShow, joints, jointPoints, dc, drawPen);
                            if(res)
                            {
                                flag = m_intFlagToShow;
                            }
                            else
                            {
                                flag = -1;
                            }

                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, m_showWidth, m_kinectDrawHeight + colorFrameOffsetY));
                }
            }

        }

        private bool JudgeWithIndex(int index, IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            DrawJoint(joints, jointPoints, drawingContext);
            bool success = false;
            if(index == 0)
            {
                success = this.DrawTargetBody_0(joints, jointPoints, drawingContext, drawingPen);
            }
            if (index == 1)
            {
                success = this.DrawTargetBody_1(joints, jointPoints, drawingContext, drawingPen);
            }
            if (index == 2)
            {
                success = this.DrawTargetBody_2(joints, jointPoints, drawingContext, drawingPen);
            }
            if (index == 3)
            {
                success = this.DrawTargetBody_3(joints, jointPoints, drawingContext, drawingPen);
            }
            if (index == 4)
            {
                success = this.DrawTargetBody_4(joints, jointPoints, drawingContext, drawingPen);
            }

            if (index == 5)
            {
                success = this.DrawTargetBody_5(joints, jointPoints, drawingContext, drawingPen);
            }

            if (index == 6)
            {
                success = this.DrawTargetBody_6(joints, jointPoints, drawingContext, drawingPen);
            }

            if (index == 7)
            {
                success = this.DrawTargetBody_7(joints, jointPoints, drawingContext, drawingPen);
            }

            if (index == 8)
            {
                success = this.DrawTargetBody_2(joints, jointPoints, drawingContext, drawingPen);
            }

            if (index == 9)
            {
                success = this.DrawTargetBody_9(joints, jointPoints, drawingContext, drawingPen);
            }

            if (index == 10)
            {
                success = this.DrawTargetBody_10(joints, jointPoints, drawingContext, drawingPen);
            }

            if (index == 11)
            {
                success = this.DrawTargetBody_4(joints, jointPoints, drawingContext, drawingPen);
            }

            if (index == 12)
            {
                success = this.DrawTargetBody_12(joints, jointPoints, drawingContext, drawingPen);
            }

            if (index == 13)
            {
                success = this.DrawTargetBody_13(joints, jointPoints, drawingContext, drawingPen);
            }
            return success;


        }

        bool CheckBone(float p, float checkLimit, out Brush drawBrush)
        {
            bool check = false;
            if (p > checkLimit)
            {
                drawBrush = new SolidColorBrush(Color.FromArgb((byte)(p * 255), 78, 204, 51));
                check = true;
            }
            else
            {
                drawBrush = new SolidColorBrush(Color.FromArgb((byte)(p * 255), 255, 255, 255));
                check = false;
            }

            return check;
        }

        private bool DrawTargetBody_0(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            bool checkAll = true;
            float checkLimit = 0.5f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.25 + Math.PI * 0.5)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;
                    
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(Math.PI * 0.25 + Math.PI * 0.5)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_Y > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(Math.PI * 0)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(Math.PI * 0.25 + Math.PI * 0.5)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_Y > 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck) ||
                    (bone.Item1 == JointType.Neck && bone.Item2 == JointType.SpineShoulder) ||
                    (bone.Item1 == JointType.SpineShoulder && bone.Item2 == JointType.SpineMid) ||
                    (bone.Item1 == JointType.SpineMid && bone.Item2 == JointType.SpineBase))
                {
                    p = Math.Abs(degreeHipToHeadCos - (Math.Cos(Math.PI * 0.33 + Math.PI)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaHipToHead_Y < 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if(p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        private bool DrawTargetBody_1(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            bool checkAll = true;
            float checkLimit = 0.5f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.5)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(Math.PI * 0.5)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_Y > 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(Math.PI * 0)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    p = deltaRShoulderToRElbow_Y < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(Math.PI * 0.25 + Math.PI)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_Y < 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck) ||
                    (bone.Item1 == JointType.Neck && bone.Item2 == JointType.SpineShoulder) ||
                    (bone.Item1 == JointType.SpineShoulder && bone.Item2 == JointType.SpineMid) ||
                    (bone.Item1 == JointType.SpineMid && bone.Item2 == JointType.SpineBase))
                {
                    p = Math.Abs(degreeHipToHeadCos - (Math.Cos(Math.PI * 0.33 + Math.PI)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaHipToHead_X < 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        private bool DrawTargetBody_2(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            //脖子到头的连线的角度
            double deltaNeckToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaNeckToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double lenghNeckToHead = Math.Sqrt(deltaNeckToHead_X * deltaNeckToHead_X + deltaNeckToHead_Y * deltaNeckToHead_Y);
            double degreeNeckToHead = deltaNeckToHead_Y / deltaNeckToHead_X;
            double degreeNeckToHeadCos = deltaNeckToHead_X / lenghNeckToHead;

            bool checkAll = true;
            float checkLimit = 0.6f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.5 + Math.PI * 0.15)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;
                    p = deltaLShoulderToLElbow_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(-Math.PI * 0.1)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_X > 0 ? p : 0;
                    p = deltaLElbowToLWrist_Y < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(Math.PI * 0.25)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    p = deltaRShoulderToRElbow_Y > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(Math.PI * 0.5 + Math.PI * 0.3)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck))
                {
                    //p = Math.Abs(degreeNeckToHeadCos - (Math.Cos(-Math.PI * 0.33)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaNeckToHead_X > 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        private bool DrawTargetBody_3(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            //脖子到头的连线的角度
            double deltaNeckToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaNeckToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double lenghNeckToHead = Math.Sqrt(deltaNeckToHead_X * deltaNeckToHead_X + deltaNeckToHead_Y * deltaNeckToHead_Y);
            double degreeNeckToHead = deltaNeckToHead_Y / deltaNeckToHead_X;
            double degreeNeckToHeadCos = deltaNeckToHead_X / lenghNeckToHead;

            bool checkAll = true;
            float checkLimit = 0.5f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos( Math.PI * 0.35)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;
                    p = deltaLShoulderToLElbow_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(Math.PI * 0.1)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_X > 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(Math.PI * 0.2)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(Math.PI * 0.45)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_X > 0 ? p : 0;
                    p = deltaRElbowToRWrist_Y > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck) ||
                    (bone.Item1 == JointType.Neck && bone.Item2 == JointType.SpineShoulder) ||
                    (bone.Item1 == JointType.SpineShoulder && bone.Item2 == JointType.SpineMid) ||
                    (bone.Item1 == JointType.SpineMid && bone.Item2 == JointType.SpineBase))
                {
                    //p = Math.Abs(degreeHipToHeadCos - (Math.Cos(-Math.PI * 0.35)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaHipToHead_X > 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        private bool DrawTargetBody_4(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            //腰子到头的连线的角度
            double deltaHipToNeck_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToNeck_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToNeck = Math.Sqrt(deltaHipToNeck_X * deltaHipToNeck_X + deltaHipToNeck_Y * deltaHipToNeck_Y);
            double degreeHipToNeck = deltaHipToNeck_Y / deltaHipToNeck_X;
            double degreeHipToNeckCos = deltaHipToNeck_X / lenghHipToNeck;


            //脖子到头的连线的角度
            double deltaNeckToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaNeckToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double lenghNeckToHead = Math.Sqrt(deltaNeckToHead_X * deltaNeckToHead_X + deltaNeckToHead_Y * deltaNeckToHead_Y);
            double degreeNeckToHead = deltaNeckToHead_Y / deltaNeckToHead_X;
            double degreeNeckToHeadCos = deltaNeckToHead_X / lenghNeckToHead;

            bool checkAll = true;
            float checkLimit = 0.5f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.65)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;
                    p = deltaLShoulderToLElbow_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(-Math.PI * 0.55)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_Y < 0 ? p : 0;
                    p = deltaLElbowToLWrist_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(Math.PI * 0.25)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_Y > 0 ? p : 0;
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(Math.PI * 0.5 + Math.PI * 0.1)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_Y > 0 ? p : 0;
                    p = deltaRElbowToRWrist_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck))
                {
                    //p = Math.Abs(degreeNeckToHeadCos - (Math.Cos(-Math.PI * 0.5)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaNeckToHead_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Neck && bone.Item2 == JointType.SpineShoulder) ||
                    (bone.Item1 == JointType.SpineShoulder && bone.Item2 == JointType.SpineMid) ||
                    (bone.Item1 == JointType.SpineMid && bone.Item2 == JointType.SpineBase))
                {
                    //p = Math.Abs(degreeHipToNeckCos - (Math.Cos(Math.PI + Math.PI * 0.45)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaHipToNeck_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        private bool DrawTargetBody_5(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            //腰子到头的连线的角度
            double deltaHipToNeck_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToNeck_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToNeck = Math.Sqrt(deltaHipToNeck_X * deltaHipToNeck_X + deltaHipToNeck_Y * deltaHipToNeck_Y);
            double degreeHipToNeck = deltaHipToNeck_Y / deltaHipToNeck_X;
            double degreeHipToNeckCos = deltaHipToNeck_X / lenghHipToNeck;


            //脖子到头的连线的角度
            double deltaNeckToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaNeckToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double lenghNeckToHead = Math.Sqrt(deltaNeckToHead_X * deltaNeckToHead_X + deltaNeckToHead_Y * deltaNeckToHead_Y);
            double degreeNeckToHead = deltaNeckToHead_Y / deltaNeckToHead_X;
            double degreeNeckToHeadCos = deltaNeckToHead_X / lenghNeckToHead;

            bool checkAll = true;
            float checkLimit = 0.5f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.5 + Math.PI * 0.2)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;
                    p = deltaLShoulderToLElbow_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(Math.PI * 0.25)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_Y > 0 ? p : 0;
                    p = deltaLElbowToLWrist_X > 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(-Math.PI * 0.25)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_Y < 0 ? p : 0;
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(-Math.PI * 0.5)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_Y < 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck))
                {
                    //p = Math.Abs(degreeNeckToHeadCos - (Math.Cos(-Math.PI * 0.5)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaNeckToHead_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Neck && bone.Item2 == JointType.SpineShoulder) ||
                    (bone.Item1 == JointType.SpineShoulder && bone.Item2 == JointType.SpineMid) ||
                    (bone.Item1 == JointType.SpineMid && bone.Item2 == JointType.SpineBase))
                {
                    //p = Math.Abs(degreeHipToNeckCos - (Math.Cos(Math.PI + Math.PI * 0.45)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaHipToNeck_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        private bool DrawTargetBody_6(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            //腰子到头的连线的角度
            double deltaHipToNeck_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToNeck_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToNeck = Math.Sqrt(deltaHipToNeck_X * deltaHipToNeck_X + deltaHipToNeck_Y * deltaHipToNeck_Y);
            double degreeHipToNeck = deltaHipToNeck_Y / deltaHipToNeck_X;
            double degreeHipToNeckCos = deltaHipToNeck_X / lenghHipToNeck;


            //脖子到头的连线的角度
            double deltaNeckToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaNeckToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double lenghNeckToHead = Math.Sqrt(deltaNeckToHead_X * deltaNeckToHead_X + deltaNeckToHead_Y * deltaNeckToHead_Y);
            double degreeNeckToHead = deltaNeckToHead_Y / deltaNeckToHead_X;
            double degreeNeckToHeadCos = deltaNeckToHead_X / lenghNeckToHead;

            bool checkAll = true;
            float checkLimit = 0.5f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.65)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;
                    p = deltaLShoulderToLElbow_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(-Math.PI * 0.55)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_Y < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(-Math.PI * 0.25)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_Y < 0 ? p : 0;
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(-Math.PI * 0.5)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_Y < 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck))
                {
                    //p = Math.Abs(degreeNeckToHeadCos - (Math.Cos(-Math.PI * 0.5)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaNeckToHead_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Neck && bone.Item2 == JointType.SpineShoulder) ||
                    (bone.Item1 == JointType.SpineShoulder && bone.Item2 == JointType.SpineMid) ||
                    (bone.Item1 == JointType.SpineMid && bone.Item2 == JointType.SpineBase))
                {
                    //p = Math.Abs(degreeHipToNeckCos - (Math.Cos(Math.PI + Math.PI * 0.45)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaHipToNeck_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        private bool DrawTargetBody_7(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            //脖子到头的连线的角度
            double deltaNeckToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaNeckToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double lenghNeckToHead = Math.Sqrt(deltaNeckToHead_X * deltaNeckToHead_X + deltaNeckToHead_Y * deltaNeckToHead_Y);
            double degreeNeckToHead = deltaNeckToHead_Y / deltaNeckToHead_X;
            double degreeNeckToHeadCos = deltaNeckToHead_X / lenghNeckToHead;

            bool checkAll = true;
            float checkLimit = 0.6f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.5)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(-Math.PI * 0.75)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_X < 0 ? p : 0;
                    p = deltaLElbowToLWrist_Y < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(Math.PI * 0.15)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    p = deltaRShoulderToRElbow_Y > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(Math.PI * 0.5 + Math.PI * 0.3)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_X < 0 ? p : 0;
                    p = deltaRElbowToRWrist_Y < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck))
                {
                    p = Math.Abs(degreeNeckToHeadCos - (Math.Cos(-Math.PI * 0.33)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaNeckToHead_X > 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        //DrawTargetBody_8 == DrawTargetBody_2

        private bool DrawTargetBody_9(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            //腰子到头的连线的角度
            double deltaHipToNeck_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToNeck_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToNeck = Math.Sqrt(deltaHipToNeck_X * deltaHipToNeck_X + deltaHipToNeck_Y * deltaHipToNeck_Y);
            double degreeHipToNeck = deltaHipToNeck_Y / deltaHipToNeck_X;
            double degreeHipToNeckCos = deltaHipToNeck_X / lenghHipToNeck;


            //脖子到头的连线的角度
            double deltaNeckToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaNeckToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double lenghNeckToHead = Math.Sqrt(deltaNeckToHead_X * deltaNeckToHead_X + deltaNeckToHead_Y * deltaNeckToHead_Y);
            double degreeNeckToHead = deltaNeckToHead_Y / deltaNeckToHead_X;
            double degreeNeckToHeadCos = deltaNeckToHead_X / lenghNeckToHead;

            bool checkAll = true;
            float checkLimit = 0.5f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.75)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;
                    p = deltaLShoulderToLElbow_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(Math.PI * 0.3)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_Y > 0 ? p : 0;
                    p = deltaLElbowToLWrist_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(Math.PI * 0.3)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_Y > 0 ? p : 0;
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(Math.PI * 1.25)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_Y < 0 ? p : 0;
                    p = deltaRElbowToRWrist_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck))
                {
                    //p = Math.Abs(degreeNeckToHeadCos - (Math.Cos(-Math.PI * 0.5)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaNeckToHead_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Neck && bone.Item2 == JointType.SpineShoulder) ||
                    (bone.Item1 == JointType.SpineShoulder && bone.Item2 == JointType.SpineMid) ||
                    (bone.Item1 == JointType.SpineMid && bone.Item2 == JointType.SpineBase))
                {
                    //p = Math.Abs(degreeHipToNeckCos - (Math.Cos(Math.PI + Math.PI * 0.45)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaHipToNeck_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        private bool DrawTargetBody_10(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            //腰子到头的连线的角度
            double deltaHipToNeck_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToNeck_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToNeck = Math.Sqrt(deltaHipToNeck_X * deltaHipToNeck_X + deltaHipToNeck_Y * deltaHipToNeck_Y);
            double degreeHipToNeck = deltaHipToNeck_Y / deltaHipToNeck_X;
            double degreeHipToNeckCos = deltaHipToNeck_X / lenghHipToNeck;


            //脖子到头的连线的角度
            double deltaNeckToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaNeckToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double lenghNeckToHead = Math.Sqrt(deltaNeckToHead_X * deltaNeckToHead_X + deltaNeckToHead_Y * deltaNeckToHead_Y);
            double degreeNeckToHead = deltaNeckToHead_Y / deltaNeckToHead_X;
            double degreeNeckToHeadCos = deltaNeckToHead_X / lenghNeckToHead;

            bool checkAll = true;
            float checkLimit = 0.5f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.75)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;
                    p = deltaLShoulderToLElbow_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(-Math.PI * 0.2)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_Y < 0 ? p : 0;
                    p = deltaLElbowToLWrist_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(Math.PI * 0.4)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_Y > 0 ? p : 0;
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(-Math.PI * 0.25)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_Y < 0 ? p : 0;
                    p = deltaRElbowToRWrist_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck))
                {
                    //p = Math.Abs(degreeNeckToHeadCos - (Math.Cos(-Math.PI * 0.5)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaNeckToHead_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Neck && bone.Item2 == JointType.SpineShoulder) ||
                    (bone.Item1 == JointType.SpineShoulder && bone.Item2 == JointType.SpineMid) ||
                    (bone.Item1 == JointType.SpineMid && bone.Item2 == JointType.SpineBase))
                {
                    //p = Math.Abs(degreeHipToNeckCos - (Math.Cos(Math.PI + Math.PI * 0.45)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaHipToNeck_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        //DrawTargetBody_11 == DrawTargetBody_4

        private bool DrawTargetBody_12(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            //腰子到头的连线的角度
            double deltaHipToNeck_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToNeck_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToNeck = Math.Sqrt(deltaHipToNeck_X * deltaHipToNeck_X + deltaHipToNeck_Y * deltaHipToNeck_Y);
            double degreeHipToNeck = deltaHipToNeck_Y / deltaHipToNeck_X;
            double degreeHipToNeckCos = deltaHipToNeck_X / lenghHipToNeck;


            //脖子到头的连线的角度
            double deltaNeckToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaNeckToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double lenghNeckToHead = Math.Sqrt(deltaNeckToHead_X * deltaNeckToHead_X + deltaNeckToHead_Y * deltaNeckToHead_Y);
            double degreeNeckToHead = deltaNeckToHead_Y / deltaNeckToHead_X;
            double degreeNeckToHeadCos = deltaNeckToHead_X / lenghNeckToHead;

            bool checkAll = true;
            float checkLimit = 0.5f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.7)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;
                    p = deltaLShoulderToLElbow_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(Math.PI * 0.8)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_Y > 0 ? p : 0;
                    p = deltaLElbowToLWrist_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(Math.PI * 0.3)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_Y > 0 ? p : 0;
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(Math.PI * 1.25)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_Y < 0 ? p : 0;
                    p = deltaRElbowToRWrist_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck))
                {
                    //p = Math.Abs(degreeNeckToHeadCos - (Math.Cos(-Math.PI * 0.5)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaNeckToHead_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Neck && bone.Item2 == JointType.SpineShoulder) ||
                    (bone.Item1 == JointType.SpineShoulder && bone.Item2 == JointType.SpineMid) ||
                    (bone.Item1 == JointType.SpineMid && bone.Item2 == JointType.SpineBase))
                {
                    //p = Math.Abs(degreeHipToNeckCos - (Math.Cos(Math.PI + Math.PI * 0.45)));
                    //p = 1 - Math.Min(p, 1);
                    //p = deltaHipToNeck_Y < 0 ? p : 0;

                    //checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }
        
        private bool DrawTargetBody_13(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            //左肩到左肘的连线
            double deltaLShoulderToLElbow_X = jointPoints[JointType.ElbowLeft].X - jointPoints[JointType.ShoulderLeft].X;
            double deltaLShoulderToLElbow_Y = jointPoints[JointType.ElbowLeft].Y - jointPoints[JointType.ShoulderLeft].Y;
            double lenghLShoulderToLElbow = Math.Sqrt(deltaLShoulderToLElbow_X * deltaLShoulderToLElbow_X + deltaLShoulderToLElbow_Y * deltaLShoulderToLElbow_Y);
            double degreeLShoulderToLElbow = deltaLShoulderToLElbow_Y / deltaLShoulderToLElbow_X;
            double degreeLShoulderToLElbowCos = deltaLShoulderToLElbow_X / lenghLShoulderToLElbow;

            //左肘到左腕的连线
            double deltaLElbowToLWrist_X = jointPoints[JointType.WristLeft].X - jointPoints[JointType.ElbowLeft].X;
            double deltaLElbowToLWrist_Y = jointPoints[JointType.WristLeft].Y - jointPoints[JointType.ElbowLeft].Y;
            double lenghLElbowToLWrist = Math.Sqrt(deltaLElbowToLWrist_X * deltaLElbowToLWrist_X + deltaLElbowToLWrist_Y * deltaLElbowToLWrist_Y);
            double degreeLElbowToLWrist = deltaLElbowToLWrist_Y / deltaLElbowToLWrist_X;
            double degreeLElbowToLWristCos = deltaLElbowToLWrist_X / lenghLElbowToLWrist;


            //右肩到右肘的连线
            double deltaRShoulderToRElbow_X = jointPoints[JointType.ElbowRight].X - jointPoints[JointType.ShoulderRight].X;
            double deltaRShoulderToRElbow_Y = jointPoints[JointType.ElbowRight].Y - jointPoints[JointType.ShoulderRight].Y;
            double lenghRShoulderToRElbow = Math.Sqrt(deltaRShoulderToRElbow_X * deltaRShoulderToRElbow_X + deltaRShoulderToRElbow_Y * deltaRShoulderToRElbow_Y);
            double degreeRShoulderToRElbow = deltaRShoulderToRElbow_Y / deltaRShoulderToRElbow_X;
            double degreeRShoulderToRElbowCos = deltaRShoulderToRElbow_X / lenghRShoulderToRElbow;

            //右肘到右腕的连线
            double deltaRElbowToRWrist_X = jointPoints[JointType.WristRight].X - jointPoints[JointType.ElbowRight].X;
            double deltaRElbowToRWrist_Y = jointPoints[JointType.WristRight].Y - jointPoints[JointType.ElbowRight].Y;
            double lenghRElbowToRWrist = Math.Sqrt(deltaRElbowToRWrist_X * deltaRElbowToRWrist_X + deltaRElbowToRWrist_Y * deltaRElbowToRWrist_Y);
            double degreeRElbowToRWrist = deltaRElbowToRWrist_Y / deltaRElbowToRWrist_X;
            double degreeRElbowToRWristCos = deltaRElbowToRWrist_X / lenghRElbowToRWrist;

            //腰到头的连线的角度
            double deltaHipToHead_X = jointPoints[JointType.Head].X - jointPoints[JointType.SpineBase].X;
            double deltaHipToHead_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.SpineBase].Y;
            double lenghHipToHead = Math.Sqrt(deltaHipToHead_X * deltaHipToHead_X + deltaHipToHead_Y * deltaHipToHead_Y);
            double degreeHipToHead = deltaHipToHead_Y / deltaHipToHead_X;
            double degreeHipToHeadCos = deltaHipToHead_X / lenghHipToHead;

            bool checkAll = true;
            float checkLimit = 0.5f;
            // Draw the bones
            foreach (var bone in this.bones)
            {
                if ((bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.HandLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.HandRight)
                    || (bone.Item1 == JointType.HandLeft && bone.Item2 == JointType.HandTipLeft)
                    || (bone.Item1 == JointType.HandRight && bone.Item2 == JointType.HandTipRight)
                    || (bone.Item1 == JointType.WristLeft && bone.Item2 == JointType.ThumbLeft)
                    || (bone.Item1 == JointType.WristRight && bone.Item2 == JointType.ThumbRight))
                {
                    continue;
                }
                drawBrush = null;

                Pen newPen = drawingPen;

                double p = -1;
                if (bone.Item1 == JointType.ShoulderLeft && bone.Item2 == JointType.ElbowLeft)
                {
                    p = Math.Abs(degreeLShoulderToLElbowCos - (Math.Cos(Math.PI * 0.65)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLShoulderToLElbow_Y > 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowLeft && bone.Item2 == JointType.WristLeft)
                {
                    p = Math.Abs(degreeLElbowToLWristCos - (Math.Cos(-Math.PI * 0.55)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaLElbowToLWrist_Y < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (bone.Item1 == JointType.ShoulderRight && bone.Item2 == JointType.ElbowRight)
                {
                    p = Math.Abs(degreeRShoulderToRElbowCos - (Math.Cos(-Math.PI * 0.2)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRShoulderToRElbow_X > 0 ? p : 0;
                    p = deltaRShoulderToRElbow_Y < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if (bone.Item1 == JointType.ElbowRight && bone.Item2 == JointType.WristRight)
                {
                    p = Math.Abs(degreeRElbowToRWristCos - (Math.Cos(-Math.PI * 0.6)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaRElbowToRWrist_Y < 0 ? p : 0;
                    p = deltaRElbowToRWrist_X < 0 ? p : 0;
                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }
                if ((bone.Item1 == JointType.Head && bone.Item2 == JointType.Neck) ||
                    (bone.Item1 == JointType.Neck && bone.Item2 == JointType.SpineShoulder) ||
                    (bone.Item1 == JointType.SpineShoulder && bone.Item2 == JointType.SpineMid) ||
                    (bone.Item1 == JointType.SpineMid && bone.Item2 == JointType.SpineBase))
                {
                    p = Math.Abs(degreeHipToHeadCos - (Math.Cos(Math.PI * 0.33 + Math.PI)));
                    p = 1 - Math.Min(p, 1);
                    p = deltaHipToHead_X < 0 ? p : 0;

                    checkAll &= CheckBone((float)p, checkLimit, out drawBrush);
                }

                if (p != -1)
                    newPen = new Pen(drawBrush, 6);

                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, newPen);
            }

            return checkAll;
        }

        private void DrawJoint(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext)
        {
            Brush drawBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                if (jointType == JointType.ThumbLeft
                    || jointType == JointType.ThumbRight
                    || jointType == JointType.HandTipLeft
                    || jointType == JointType.HandTipRight
                    || jointType == JointType.HandLeft
                    || jointType == JointType.HandRight)
                    continue;
                drawBrush = null;

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

        void timer_Tick(object sender, EventArgs e)
        {
            int tempTimer = m_timer;
            m_timer = Environment.TickCount - m_startTime;
            m_deltaTime = m_timer - tempTimer;

            m_checkDelay += m_deltaTime;

            scrollTimer += m_deltaTime;

            if(m_bodyTracked)
            {
                bodyTrackTimer += m_deltaTime;
                if (bodyTrackTimer >= 10 * bodyTrackTime)
                    bodyTrackTimer = 10 * bodyTrackTime;
            }
            else{
                bodyTrackTimer -= m_deltaTime;
                if (bodyTrackTimer <= -10 * bodyTrackTime)
                    bodyTrackTimer = -10 * bodyTrackTime;
            }
            Console.WriteLine(bodyTrackTimer);

            UpdateInfo();

            //this.Title = string.Concat("TimerWindow  ", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));
            SelfController();
        }

        private void UpdateInfo()
        {

            for (int i = 0; i < stackCount - 1; i++)
            {
                moveStack[i] = moveStack[i + 1];
            }
            moveStack[stackCount - 1] = flag;

            int count = 0;
            for (int i = 0; i < stackCount - 1; i++)
            {
                if (moveStack[i] == flag)
                    count++;
            }
            int showFlag = -1;
            if (count > stackCount * 0.7f)
            {
                showFlag = flag;
            }

            using (DrawingContext dc = this.drawingPicGroup.Open())
            {
                DrawShow(dc, showFlag);

            }
        }


        float bodyTrackTimer = 0;
        float bodyTrackTime = 1000;

        int m_intFlagToShow = 0;

        float texFadeTimer = 0;
        float texFadeTime = 1000;

        float scrollTimer = 200000;
        float scrollOpenTime = 1000;
        float scrollStayTime = 3000;
        float infoFadeInTime = 3300;
        float infoFadeOutTime = 6700 + 9000;
        float scrollCloseTime = 7000 + 9000;
        float scrollOverTime = 9000 + 9000;


        System.Drawing.Image srcImage;
        System.Drawing.Bitmap resultImage;
        System.Drawing.Graphics g;

        bool m_lastChecked = false;

        enum ScrollState
        {
            Open,
            Stay,
            Close,
            None
        }
        ScrollState scrollSate = ScrollState.None;

        ScrollState lastScrollSate = ScrollState.None;


        bool checkStep = false;

        int minTexNo = 0;
        int maxTexNo = 13;
        void DrawShow(DrawingContext dc, int flag)
        {
            m_maxDelay = (int)scrollOverTime + 1700;
            if (TexList.Count < 0)
            {
                return;
            }

            if (!hasInitImage)
            {
                for (int i = 0; i < TexList.Count; i++)
                {
                    dc.DrawImage(TexList[i], new Rect(0, 0, m_showWidth, m_showHeight));
                }
                hasInitImage = true;
            }

            //======================================================================================================

            m_lastChecked = m_hasChecked;

            //如果评价返回的是-1（flag < 0） 并且显示时间大于最大显示时间，变成false
            if (m_checkDelay > m_maxDelay)
            {
                if(flag < 0)
                {
                    m_hasChecked = false;
                }
                else
                {
                    m_drawFlag = flag;
                    m_hasChecked = true;
                    m_checkDelay = 0;
                }


                if (!m_lastChecked && m_hasChecked)
                {
                    scrollTimer = 0;
                    scrollAnim.ResetAnim();
                }

                //如果有人的话，显示提示用的图
                //并且确定这一轮要用哪张图
                if ((m_bodyTracked && !m_lastBodyTracked) || (m_lastChecked && !m_hasChecked))
                {
                    checkStep = true;

                    Random random = new Random();
                    int randomNumber = random.Next(minTexNo, maxTexNo + 1);
                    randomNumber = randomNumber > 13 ? 13 : randomNumber;
                    randomNumber = randomNumber < 0 ? 0 : randomNumber;
                    m_intFlagToShow = randomNumber;
                }
            }

            if (!checkStep && m_bodyTracked)
            {
                checkStep = true;
                Random random = new Random();
                int randomNumber = random.Next(minTexNo, maxTexNo + 1);
                randomNumber = randomNumber > 13 ? 13 : randomNumber;
                randomNumber = randomNumber < 0 ? 0 : randomNumber;
                m_intFlagToShow = randomNumber;
            }


            //渲染一个黑色的背景
            dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, 1, 1));

            
            

            //渲染下方的提示图
            dc.DrawImage(HintList[m_intFlagToShow], new Rect(0, 0, m_showWidth, m_showHeight));
            //渲染正中的信息提示
            dc.DrawImage(startInfoTex, new Rect(startInfoX, startInfoY, startInfoWidth, startInfoHeight));

            //m_lastBodyTracked = m_bodyTracked;


            if(m_lastBodyTracked && !m_bodyTracked)
            {
                bodyTrackTimer = bodyTrackTime;
            }

            if (!m_lastBodyTracked && m_bodyTracked)
            {
                bodyTrackTimer = 0;
            }

            SolidColorBrush bgBlackBrush = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));

            //渲染渐变的黑色遮罩
            //有人接近的时候产生淡入效果，离开产生淡出效果
            float p_bg = 0;
            float alpha = 255;
            if (m_bodyTracked)
            {
                if (bodyTrackTimer <= bodyTrackTime)
                {
                    if (bodyTrackTimer < 0)
                    {
                        bodyTrackTimer = 0;
                    }
                    p_bg = bodyTrackTimer / bodyTrackTime;
                }
                else
                {
                    p_bg = 1;
                }
            }
            else
            {
                if (bodyTrackTimer >= 0)
                {
                    p_bg = bodyTrackTimer / bodyTrackTime;
                }
                else
                {
                    p_bg = 0;
                }
            }

            alpha = (1 - p_bg * 1.0f) * 255;
            //alpha *= 0.5f;
            bgBlackBrush = new SolidColorBrush(Color.FromArgb((byte)alpha, 0, 0, 0));

            dc.DrawRectangle(bgBlackBrush, null, new Rect(0.0, 0.0, m_showWidth, m_showHeight));


            //渲染预览的小人动图
            if (!m_bodyTracked && bodyTrackTimer <= 0)
            {
                SolidColorBrush previewBrush = new SolidColorBrush(Color.FromArgb(178, 0, 0, 0));
                for (int i = 0; i < m_PreviewList.Count; i++)
                {
                    if (i != m_drawFlag && m_hasChecked == true)
                        continue;

                    float x = m_PreviewList[i].PosX;
                    float y = m_PreviewList[i].PosY;


                    //dc.DrawRectangle(previewBrush, null, new Rect(x, y, iconSize,iconSize));
                    //dc.DrawImage(MoveImageList[i], new Rect(x, y, iconSize, iconSize));
                    //if(i == 1)
                    {
                        m_PreviewList[i].DrawAnimPic(dc, m_deltaTime, x, y, iconSize, iconSize);
                    }
                }
                dc.DrawImage(titleTex, new Rect(titleX, titleY, titleWidth, titleHeight));
                
            }



            //主体的渲染逻辑
            
            float p_tex = 0;
            if (m_hasChecked)
            {
                if (texFadeTimer < texFadeTime)
                {
                    texFadeTimer += m_deltaTime;

                    p_tex = texFadeTimer / texFadeTime;
                }
                else
                {
                    p_tex = 1;
                }
            }
            else
            {
                if (texFadeTimer > 0)
                {
                    texFadeTimer -= m_deltaTime;

                    p_tex = texFadeTimer / texFadeTime;
                }
                else
                {
                    p_tex = 0;
                }
            }


            if ((m_hasChecked || texFadeTimer > 0) && m_drawFlag >= 0 && m_drawFlag < TexList.Count)
            {

                if (p_tex < 0.99f)
                {
                    float opacity = p_tex;
                    DrawImageBlend(dc, TexImageList[m_drawFlag], opacity,0,0,m_showWidth, m_showHeight);
                }
                else
                {
                    dc.DrawImage(TexList[m_drawFlag], new Rect(0, 0, m_showWidth, m_showHeight));
                }

            }
     
            //Geometry ellipse = new EllipseGeometry(new Point(200, 70), 100, 50);
            //GeometryDrawing drawing = new GeometryDrawing(Brushes.LightBlue, new Pen(Brushes.Green, 1), ellipse);
            //dc.DrawDrawing(drawing);
            
            //dc.DrawEllipse(Brushes.Red, null, new Point(m_showWidth - 50, 0), 50, 50);

            SolidColorBrush bgBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));


            //绘制洞窟外框
            //dc.DrawImage(stoneBG, new Rect(0, 0, m_showWidth, m_showHeight));

            //=========================================================================
            
            lastScrollSate = scrollSate;

            if (scrollTimer >= 0 && scrollTimer < scrollOpenTime)
            {
                scrollSate = ScrollState.None;
            }
            if (scrollTimer >= scrollOpenTime && scrollTimer < scrollStayTime)
            {
                scrollSate = ScrollState.Open;
            }
            if (scrollTimer >= scrollStayTime && scrollTimer < scrollCloseTime)
            {
                scrollSate = ScrollState.Stay;
            }
            if (scrollTimer >= scrollCloseTime && scrollTimer < scrollOverTime)
            {
                scrollSate = ScrollState.Close;
            }
            if (scrollTimer >= scrollOverTime)
            {
                scrollSate = ScrollState.None;
            }


            if(scrollSate == ScrollState.None)
            {

            }
            else if (scrollSate == ScrollState.Open)
            {
                scrollAnim.DrawAnimPic_Clamp(dc, m_deltaTime, infoX, infoY, infoWidth, infoHeight, 0, 19);
            }
            else if (scrollSate == ScrollState.Stay)
            {
                scrollAnim.DrawAnimPic_Clamp(dc, m_deltaTime, infoX, infoY, infoWidth, infoHeight, 20, 38);
                //dc.DrawImage(InfoTexList[m_drawFlag], new Rect(infoX, infoY, infoWidth, infoHeight));
                float infoAlpha = 0;
                if (scrollTimer < infoFadeInTime)
                {
                    infoAlpha = (scrollTimer - scrollStayTime) / (infoFadeInTime - scrollStayTime);
                }
                else if (scrollTimer < infoFadeOutTime)
                {
                    infoAlpha = 1;
                }
                else
                {
                    infoAlpha = (scrollTimer - infoFadeOutTime) / (scrollCloseTime - infoFadeOutTime);
                    infoAlpha = 1 - infoAlpha;
                }
                infoAlpha = infoAlpha > 1f ? 1 : infoAlpha;
                infoAlpha = infoAlpha < 0f ? 0f : infoAlpha;
                if (infoAlpha == 1.0)
                {
                    dc.DrawImage(InfoTexList[m_drawFlag], new Rect(infoX, infoY, infoWidth, infoHeight));
                }
                else
                {
                    DrawImageBlend(dc, InfoTexImageList[m_drawFlag], infoAlpha, infoX, infoY, infoWidth, infoHeight, true);
                }
                
                
            }
            else if (scrollSate == ScrollState.Close)
            {
                scrollAnim.DrawAnimPic_Clamp(dc, m_deltaTime, infoX, infoY, infoWidth, infoHeight, 39, 60);
            }
        }

        private void DrawImageBlend(DrawingContext dc, System.Drawing.Image image, float alpha, float x, float y, float width, float height, bool makeTransparent = false)
        {
            if (makeTransparent)
            {
                image = TransparentImage(image);
            }

            float[][] nArray = { 
            new float[] {1, 0, 0, 0, 0},
            new float[] {0, 1, 0, 0, 0},
            new float[] {0, 0, 1, 0, 0},
            new float[] {0, 0, 0, 1, 0},
            new float[] {0, 0, 0, 0, 1}
            };
            nArray[3][3] = alpha;
            System.Drawing.Imaging.ColorMatrix matrix = new System.Drawing.Imaging.ColorMatrix(nArray);
            ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            attributes.ClearColorKey();
            //srcImage = (System.Drawing.Image)BitmapImage2Bitmap(ImageList[m_drawFlag]);
            resultImage = new System.Drawing.Bitmap(image.Width, image.Height);
            g = System.Drawing.Graphics.FromImage(resultImage);

            g.DrawImage(image, new System.Drawing.Rectangle(0, 0, image.Width, image.Height), 0, 0, image.Width, image.Height, System.Drawing.GraphicsUnit.Pixel, attributes);

            dc.DrawImage(Bitmap2BitmapImage(resultImage), new Rect(x, y, width, height));
            
        }

        private System.Drawing.Bitmap TransparentImage(System.Drawing.Image srcImage)
        {
            ImageAttributes attributes = new ImageAttributes();
            System.Drawing.Bitmap resultImage = new System.Drawing.Bitmap(srcImage.Width, srcImage.Height);
            System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(resultImage);
            g.DrawImage(srcImage, new System.Drawing.Rectangle(0, 0, srcImage.Width, srcImage.Height), 0, 0, srcImage.Width, srcImage.Height, System.Drawing.GraphicsUnit.Pixel, attributes);
            resultImage.MakeTransparent(System.Drawing.Color.White);
            return resultImage;
        } 


        private System.Drawing.Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            // BitmapImage bitmapImage = new BitmapImage(new Uri("../Images/test.png", UriKind.Relative));

            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

                return new System.Drawing.Bitmap(bitmap);
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private BitmapSource Bitmap2BitmapImage(System.Drawing.Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            BitmapSource retval;

            try
            {
                retval = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                             hBitmap,
                             IntPtr.Zero,
                             Int32Rect.Empty,
                             BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }

            return retval;
        }

        public object Convert(object value)
        {
            MemoryStream ms = new MemoryStream();
            ((System.Drawing.Bitmap)value).Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();

            return image;
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
            
            //头到脖子的连线的角度
            double deltaHeadToShoulderCenter_X = jointPoints[JointType.Head].X - jointPoints[JointType.Neck].X;
            double deltaHeadToShoulderCenter_Y = jointPoints[JointType.Head].Y - jointPoints[JointType.Neck].Y;
            double degreeHeadToShoulderCenter = Math.Abs(Math.Atan(deltaHeadToShoulderCenter_Y / deltaHeadToShoulderCenter_X));

            //1,2号图片
            if (degreeHeadToHip >= Math.PI * 0.166 && degreeHeadToHip <= Math.PI * 0.4)
            {
                if (deltaHeadToHip_X < 0)
                {
                    if(degreeLHandToShoulder >= Math.PI * 0.15 && degreeLHandToShoulder <= Math.PI * 0.33)
                    {
                        if (deltaLHandToShoulder_X < 0 && deltaLHandToShoulder_Y > 0)
                        {
                            if (degreeRElbowToShoulder <= Math.PI * 0.25)
                            {
                                if(jointPoints[JointType.HandRight].X < jointPoints[JointType.ElbowRight].X
                                && jointPoints[JointType.HandRight].Y > jointPoints[JointType.ShoulderRight].Y)
                                {
                                   return 0;
                                }
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

            //3号图
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

            //4号图
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
                    if (degreeRElbowToShoulder < Math.PI * 0.6)
                    {
                        if (/*deltaRElbowToShoulder_X < 0 &&*/ deltaRElbowToShoulder_Y > 0)
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

            //5号图
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

            //7号图
            if (deltaHeadToShoulderCenter_X > 0 && degreeHeadToShoulderCenter < 0.47 * Math.PI)
            {
                if (degreeLElbowToShoulder >= 0.25 * Math.PI && degreeRElbowToShoulder <= 0.25 * Math.PI)
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


            //8号图
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

            //9号图
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

            //10号图
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

            //11号图
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

            //12号图
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

            //13号图
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

            //14号图
            if (deltaHeadToHip_X < 0 && degreeHeadToHip < 0.47 *Math.PI)
            {
                if (deltaLElbowToShoulder_Y > 0 && deltaRElbowToShoulder_Y < 0)
                {
                    if (degreeLElbowToShoulder >= 0.35 * Math.PI && degreeRElbowToShoulder >= 0.25 * Math.PI && degreeRElbowToShoulder <= 0.4 * Math.PI)
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

        private bool LoadPic()
        {
            if (TexList == null)
            {
                TexList = new List<BitmapImage>();
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
            TexList.Clear();

            for (int i = 0; i < imgCount; i++)
            {
                String imgPath = fileList[i];
                bool isExist = File.Exists(imgPath.ToString());
                if (isExist)
                {
                    BitmapImage newImage = new BitmapImage(new Uri(imgPath, UriKind.Absolute));
                    TexList.Add(newImage);
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

        private bool LoadPicImage(string folderName, out List<System.Drawing.Image> texList)
        {
            texList = new List<System.Drawing.Image>();

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
                    System.Drawing.Image srcImage = (System.Drawing.Image)BitmapImage2Bitmap(newImage);
                    texList.Add(srcImage);
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

    }
}
