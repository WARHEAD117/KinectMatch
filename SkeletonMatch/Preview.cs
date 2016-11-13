using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Kinect;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Windows.Input;

namespace Microsoft.Samples.Kinect.SkeletonRecord
{
    
    class Preview
    {

        float defPosX;
        float defPosY;


        public Preview(float x, float y)
        {
            defPosX = x;
            defPosY = y;
            PosX = defPosX;
            PosY = defPosY;
            index = 0;
        }


        int index;
        float delay;
        int timer;
        List<BitmapImage> moveImageList;

        public Preview(float x, float y, string fileFolder)
        {
            defPosX = x;
            defPosY = y;
            PosX = defPosX;
            PosY = defPosY;
            index = 0;
            delay = 50;
            timer = 0;

            if(fileFolder != null && fileFolder != "")
            {
                LoadPic(fileFolder, out moveImageList);
            }
        }

        public float PosX
        {
            get;
            set;
        }

        public float PosY
        {
            get;
            set;
        }

        public void ResetPos()
        {
            PosY = defPosY;
            PosX = defPosX;
        }

        public void DrawAnimPic(DrawingContext dc, int dt, double x, double y, double w, double h)
        {
            timer += dt;
            if(timer >= delay)
            {
                index += 1;
                timer = 0;
            }

            if (moveImageList == null)
                return;

            if (index < 0)
                index = 0;
            if (index >= moveImageList.Count)
                index = 0;

            if (moveImageList.Count > 0 && index < moveImageList.Count)
                dc.DrawImage(moveImageList[index], new Rect(x, y, w, h));
        }

        public void DrawAnimPic_Clamp(DrawingContext dc, int dt, double x, double y, double w, double h, int startFrame, int endFrame)
        {
            if (startFrame > endFrame)
                startFrame = endFrame;
            if (startFrame < 0)
                startFrame = 0;
            if (endFrame >= moveImageList.Count)
                endFrame = moveImageList.Count - 1;
            if (startFrame >= moveImageList.Count)
                startFrame = moveImageList.Count - 1;
            if (endFrame < 0)
                endFrame = 0;

            timer += dt;
            if (timer >= delay)
            {
                index += 1;
                if (index > endFrame)
                    index = endFrame;
                if (index < startFrame)
                    index = startFrame;
                timer = 0;
            }

            if (moveImageList == null)
                return;

            if (index < 0)
                index = 0;
            if (index >= moveImageList.Count)
                index = moveImageList.Count;
            

            if (moveImageList.Count > 0 && index < moveImageList.Count)
                dc.DrawImage(moveImageList[index], new Rect(x, y, w, h));
        }

        public void ResetAnim()
        {
            index = 0;
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
    }
}
