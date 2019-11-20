using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ProjectOxford.Common.Contract;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System.Linq;
using WMPLib;

namespace FaceTutorial
{
    public partial class MainWindow : Window
    {
        
        private readonly IFaceServiceClient faceServiceClient =
            new FaceServiceClient("25a4ca213cbe4a0cab2fbe37b5a317a1", "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");

        Face[] faces;                   // The list of detected faces.
        String[] faceDescriptions;      // The list of descriptions for the detected faces.
        double resizeFactor;            // The resize factor for the displayed image.

        public MainWindow()
        {
            InitializeComponent();
        }

        // Displays the image and calls Detect Faces.

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            //Stop Music Player if it's being played already.
            player.Stop();

            // Get the image file to scan from the user.
            var openDlg = new Microsoft.Win32.OpenFileDialog();

            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);

            // Return if canceled.
            if (!(bool)result)
            {
                return;
            }

            // Display the image file.
            string filePath = openDlg.FileName;

            Uri fileUri = new Uri(filePath);
            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            FacePhoto.Source = bitmapSource;

            // Detect any faces in the image.
            Title = "Detecting...";
            faces = await UploadAndDetectFaces(filePath);
            Title = String.Format("Detection Finished. {0} face(s) detected", faces.Length);
            
            if (faces.Length > 0)
            {
                // Prepare to draw rectangles around the faces.
                DrawingVisual visual = new DrawingVisual();
                DrawingContext drawingContext = visual.RenderOpen();
                drawingContext.DrawImage(bitmapSource,
                    new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));

                
                double dpi = bitmapSource.DpiX;
                resizeFactor = 96 / dpi;
                faceDescriptions = new String[faces.Length];

                for (int i = 0; i < faces.Length; ++i)
                {
                    Face face = faces[i];

                    /*
                    // Draw a rectangle on the face.
                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(Brushes.Red, 2),
                        new Rect(
                            face.FaceRectangle.Left * resizeFactor,
                            face.FaceRectangle.Top * resizeFactor,
                            face.FaceRectangle.Width * resizeFactor,
                            face.FaceRectangle.Height * resizeFactor
                            )
                    );
                    */

                    // Store the face description.
                    faceDescriptions[i] = FaceDescription(face);

                    //Find the Max Emotion Value
                    EmotionScores emotionScores = face.FaceAttributes.Emotion;
                    Dictionary<string, float> dict = new Dictionary<string, float>();
                    dict["anger"] = emotionScores.Anger;
                    dict["contempt"] = emotionScores.Contempt;
                    dict["disgust"] = emotionScores.Disgust;
                    dict["fear"] = emotionScores.Fear;
                    dict["happiness"] = emotionScores.Happiness;
                    dict["neutral"] = emotionScores.Neutral;
                    dict["sadness"] = emotionScores.Sadness;
                    dict["surprise"] = emotionScores.Surprise;

                    var EmotionNameForMaxEmotionValue = dict.FirstOrDefault(x => x.Value == dict.Values.Max()).Key;

                    var path = "Emojis/"+EmotionNameForMaxEmotionValue+".png";

                    drawingContext.DrawImage(new BitmapImage(new Uri(path,UriKind.Relative)), new Rect(
                        face.FaceRectangle.Left * resizeFactor,
                        face.FaceRectangle.Top * resizeFactor,
                        face.FaceRectangle.Width * resizeFactor,
                        face.FaceRectangle.Height * resizeFactor)
                    );

                    
                }

                drawingContext.Close();

                // Display the image with the rectangle around the face.
                RenderTargetBitmap faceWithRectBitmap = new RenderTargetBitmap(
                    (int)(bitmapSource.PixelWidth * resizeFactor),
                    (int)(bitmapSource.PixelHeight * resizeFactor),
                    96,
                    96,
                    PixelFormats.Pbgra32);

                faceWithRectBitmap.Render(visual);
                FacePhoto.Source = faceWithRectBitmap;

                // Set the status bar text.
                faceDescriptionStatusBar.Text = "Place the mouse pointer over a face to see the face description.";
            }
        }

        // Displays the face description when the mouse is over a face rectangle.

        private void FacePhoto_MouseMove(object sender, MouseEventArgs e)
        {
            // If the REST call has not completed, return from this method.
            if (faces == null)
                return;

            // Find the mouse position relative to the image.
            Point mouseXY = e.GetPosition(FacePhoto);

            ImageSource imageSource = FacePhoto.Source;
            BitmapSource bitmapSource = (BitmapSource)imageSource;

            // Scale adjustment between the actual size and displayed size.
            var scale = FacePhoto.ActualWidth / (bitmapSource.PixelWidth / resizeFactor);

            // Check if this mouse position is over a face rectangle.
            bool mouseOverFace = false;

            for (int i = 0; i < faces.Length; ++i)
            {
                FaceRectangle fr = faces[i].FaceRectangle;
                double left = fr.Left * scale;
                double top = fr.Top * scale;
                double width = fr.Width * scale;
                double height = fr.Height * scale;

                // Display the face description for this face if the mouse is over this face rectangle.
                if (mouseXY.X >= left && mouseXY.X <= left + width && mouseXY.Y >= top && mouseXY.Y <= top + height)
                {
                    faceDescriptionStatusBar.Text = faceDescriptions[i];
                    mouseOverFace = true;
                    break;
                }
            }

            // If the mouse is not over a face rectangle.
            if (!mouseOverFace)
                faceDescriptionStatusBar.Text = "Place the mouse pointer over a face to see the face description (or)" +
                    "Click a face to play music!";
        }

        // Uploads the image file and calls Detect Faces.

        private async Task<Face[]> UploadAndDetectFaces(string imageFilePath)
        {
            // The list of Face attributes to return.
            IEnumerable<FaceAttributeType> faceAttributes =
                new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Emotion, FaceAttributeType.Glasses, FaceAttributeType.Hair };

            // Call the Face API.
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    Face[] faces = await faceServiceClient.DetectAsync(imageFileStream, returnFaceId: true, returnFaceLandmarks: false, returnFaceAttributes: faceAttributes);
                    return faces;
                }
            }
            // Catch and display Face API errors.
            catch (FaceAPIException f)
            {
                MessageBox.Show(f.ErrorMessage, f.ErrorCode);
                return new Face[0];
            }
            // Catch and display all other errors.
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                return new Face[0];
            }
        }

        // Returns a string that describes the given face.

        private string FaceDescription(Face face)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("Face: ");

            // Add the gender, age, and smile.
            sb.Append(face.FaceAttributes.Gender);
            sb.Append(", ");
            sb.Append(face.FaceAttributes.Age);
            sb.Append(", ");
            sb.Append(String.Format("smile {0:F1}%, ", face.FaceAttributes.Smile * 100));

            // Add the emotions. Display all emotions over 10%.
            sb.Append("Emotion: ");
            EmotionScores emotionScores = face.FaceAttributes.Emotion;
            //if (emotionScores.Anger >= 0.1f)
                sb.Append(String.Format("anger {0:F1}%, ", emotionScores.Anger * 100));
            //if (emotionScores.Contempt >= 0.1f)
                sb.Append(String.Format("contempt {0:F1}%, ", emotionScores.Contempt * 100));
            //if (emotionScores.Disgust >= 0.1f)
                sb.Append(String.Format("disgust {0:F1}%, ", emotionScores.Disgust * 100));
            //if (emotionScores.Fear >= 0.1f)
                sb.Append(String.Format("fear {0:F1}%, ", emotionScores.Fear * 100));
            //if (emotionScores.Happiness >= 0.1f)
                sb.Append(String.Format("happiness {0:F1}%, ", emotionScores.Happiness * 100));
            //if (emotionScores.Neutral >= 0.1f)
                sb.Append(String.Format("neutral {0:F1}%, ", emotionScores.Neutral * 100));
            //if (emotionScores.Sadness >= 0.1f)
                sb.Append(String.Format("sadness {0:F1}%, ", emotionScores.Sadness * 100));
            //if (emotionScores.Surprise >= 0.1f)
                sb.Append(String.Format("surprise {0:F1}%, ", emotionScores.Surprise * 100));

            Dictionary<string, float> dict = new Dictionary<string, float>();
            dict["anger"] = emotionScores.Anger;
            dict["contempt"] = emotionScores.Contempt;
            dict["disgust"] = emotionScores.Disgust;
            dict["fear"] = emotionScores.Fear;
            dict["happiness"] = emotionScores.Happiness;
            dict["neutral"] = emotionScores.Neutral;
            dict["sadness"] = emotionScores.Sadness;
            dict["surprise"] = emotionScores.Surprise;

            var EmotionNameForMaxEmotionValue = dict.FirstOrDefault(x => x.Value == dict.Values.Max()).Key;
            string facelooks = "The face looks ";
            sb.Append(facelooks + EmotionNameForMaxEmotionValue);

            // Add glasses.
            sb.Append(face.FaceAttributes.Glasses);
            sb.Append(", ");

            // Add hair.
            sb.Append("Hair: ");

            // Display baldness confidence if over 1%.
            if (face.FaceAttributes.Hair.Bald >= 0.01f)
                sb.Append(String.Format("bald {0:F1}% ", face.FaceAttributes.Hair.Bald * 100));

            // Display all hair color attributes over 10%.
            HairColor[] hairColors = face.FaceAttributes.Hair.HairColor;
            foreach (HairColor hairColor in hairColors)
            {
                if (hairColor.Confidence >= 0.1f)
                {
                    sb.Append(hairColor.Color.ToString());
                    sb.Append(String.Format(" {0:F1}% ", hairColor.Confidence * 100));
                }
            }

            // Return the built string.
            return sb.ToString();
        }

        MediaPlayer player = new MediaPlayer();

        private void FacePhoto_MouseDown(object sender, MouseButtonEventArgs e)
        {

            player.Stop();

            // If the REST call has not completed, return from this method.
            if (faces == null)
                return;

            // Find the mouse position relative to the image.
            Point mouseXY = e.GetPosition(FacePhoto);

            ImageSource imageSource = FacePhoto.Source;
            BitmapSource bitmapSource = (BitmapSource)imageSource;

            // Scale adjustment between the actual size and displayed size.
            var scale = FacePhoto.ActualWidth / (bitmapSource.PixelWidth / resizeFactor);

            // Check if this mouse position is over a face rectangle.
            bool mouseOverFace = false;

            for (int i = 0; i < faces.Length; ++i)
            {
                FaceRectangle fr = faces[i].FaceRectangle;
                double left = fr.Left * scale;
                double top = fr.Top * scale;
                double width = fr.Width * scale;
                double height = fr.Height * scale;

                // Display the face description for this face if the mouse is over this face rectangle.
                if (mouseXY.X >= left && mouseXY.X <= left + width && mouseXY.Y >= top && mouseXY.Y <= top + height)
                {
                    faceDescriptionStatusBar.Text = faceDescriptions[i];
                    mouseOverFace = true;

                    EmotionScores emotionScores = faces[i].FaceAttributes.Emotion;
                    Dictionary<string, float> dict = new Dictionary<string, float>();
                    dict["anger"] = emotionScores.Anger;
                    dict["contempt"] = emotionScores.Contempt;
                    dict["disgust"] = emotionScores.Disgust;
                    dict["fear"] = emotionScores.Fear;
                    dict["happiness"] = emotionScores.Happiness;
                    dict["neutral"] = emotionScores.Neutral;
                    dict["sadness"] = emotionScores.Sadness;
                    dict["surprise"] = emotionScores.Surprise;

                    var EmotionNameForMaxEmotionValue = dict.FirstOrDefault(x => x.Value == dict.Values.Max()).Key;

                    playMusic(EmotionNameForMaxEmotionValue);
                    
                    break;
                }
            }           
            
        }

        private void playMusic(string emotionName)
        {
            WindowsMediaPlayer wmp = new WindowsMediaPlayer();
            
            if (emotionName.CompareTo("happiness") == 0)
            {
                var uri = new Uri(@"D:\Songs\[iSongs.info] 02 - Varsham Munduga.mp3", UriKind.Relative);
                player.Open(uri);
                player.Play();
            }
            else if(emotionName.CompareTo("sadness")==0)
            {
                var uri = new Uri(@"D:\Songs\Believer - 128Kbps.mp3", UriKind.Relative);
                player.Open(uri);
                player.Play();
            }
            else if(emotionName.CompareTo("anger")==0)
            {
                var uri = new Uri(@"D:\Songs\Alex Sparrow - She's Crazy But She's Mine - English Version ( Lyrics Video ).mp4", UriKind.Relative);
                player.Open(uri);
                player.Play();
            }
            else if(emotionName.CompareTo("contempt")==0)
            {
                var uri = new Uri(@"D:\Songs\Ariana Grande - Side To Side ft. Nicki Minaj.mp3", UriKind.Relative);
                player.Open(uri);
                player.Play();
            }
            else if(emotionName.CompareTo("disgust")==0)
            {
                var uri = new Uri(@"D:\Songs\In The Name Of Love.mp3", UriKind.Relative);
                player.Open(uri);
                player.Play();
            }
            else if(emotionName.CompareTo("neutral")==0)
            {
                var uri = new Uri(@"D:\Songs\Bridgit Mendler - Ready or Not.mp3", UriKind.Relative);
                player.Open(uri);
                player.Play();
            }
            else if(emotionName.CompareTo("fear")==0)
            {
                var uri = new Uri(@"D:\Songs\Clean Bandit - Rockabye ft. Sean Paul.mp3", UriKind.Relative);
                player.Open(uri);
                player.Play();
            }
            else if(emotionName.CompareTo("surprise")==0)
            {
                var uri = new Uri(@"D:\Songs\Cheat Codes - No Promises ft. Demi Lovato [Official Video].mp3", UriKind.Relative);
                player.Open(uri);
                player.Play();
            }            
        }
    }
}