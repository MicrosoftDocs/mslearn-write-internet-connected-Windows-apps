using System;
using System.IO;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Windows.Data.Json;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Astronomy_Picture_of_the_Day_UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // The objective of the NASA API portal is to make NASA data, including imagery, eminently accessible to application developers. 
        const string EndpointURL = "https://api.nasa.gov/planetary/apod";

        // June 16, 1995  : the APOD launch date.
        DateTime launchDate = new DateTime(1995, 6, 16);

        // A count of images downloaded today.
        int imageCountToday;

        // Settings name strings, used to preserve UI values between sessions.
        const string SettingDateToday = "date today";
        const string SettingShowOnStartup = "show on startup";
        const string SettingImageCountToday = "image count today";
        const string SettingLimitRange = "limit range";

        // Declare a container for the local settings.
        Windows.Storage.ApplicationDataContainer localSettings;

        public MainPage()
        {
            this.InitializeComponent();

            // Set the maximum date to today, and the minimum date to the date APOD was launched.
            MonthCalendar.MinDate = launchDate;
            MonthCalendar.MaxDate = DateTime.Today;

            // Create the container for the local settings.
            localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            ReadSettings();
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            // Make sure the full range of dates is available.
            LimitRangeCheckBox.IsChecked = false;

            // This will not load up the image, just sets the calendar to the APOD launch date.
            MonthCalendar.Date = launchDate;
        }

        private void LimitRangeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Set the calendar minimum date to the first of the current year.
            var firstDayOfThisYear = new DateTime(DateTime.Today.Year, 1, 1);
            MonthCalendar.MinDate = firstDayOfThisYear;
        }

        private void LimitRangeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Set the calendar minimum date to the launch of the APOD program.
            MonthCalendar.MinDate = launchDate;
        }

        private async void MonthCalendar_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            await RetrievePhotoAsync();
        }

        private bool IsSupportedFormat(string photoURL)
        {
            // Extract the extension and force to lower case for comparison purposes.
            string ext = Path.GetExtension(photoURL).ToLower();

            // Check the extension against supported UWP formats.
            return (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" ||
                    ext == ".tif" || ext == ".bmp" || ext == ".ico" || ext == ".svg");
        }

        private async Task RetrievePhotoAsync()
        {
            var client = new HttpClient();
            string responseContent = null;

            // Build the date parameter string for the date selected, or the last date if a range is specified.
            var dt = (DateTimeOffset)MonthCalendar.Date;

            string dateSelected = $"{dt.Year.ToString()}-{dt.Month.ToString("00")}-{dt.Day.ToString("00")}";
            string URLParams = $"?date={dateSelected}&api_key=DEMO_KEY";

            // Populate the Http client appropriately.
            client.BaseAddress = new Uri(EndpointURL);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // The critical call: sends a GET request with the appropriate parameters.
            HttpResponseMessage response = client.GetAsync(URLParams).Result;

            if (response.IsSuccessStatusCode)
            {
                // Be ready to catch any data/server errors.
                try
                {
                    // Wait for the response string.
                    responseContent = await response.Content.ReadAsStringAsync();

                    // Parse the response string for the details we need.
                    JsonObject jsonObj = JsonObject.Parse(responseContent);

                    // The try/catch block will throw an exception if no url is found. For copyright and description, default values are used.
                    string imageUrl = jsonObj.GetNamedString("url");
                    string imageCopyright = jsonObj.GetNamedString("copyright", "NASA");
                    string imageDescription = jsonObj.GetNamedString("explanation", "No data");

                    // Now create a bitmap for the image.
                    var photoURI = new Uri(imageUrl);
                    var bmi = new BitmapImage(photoURI);

                    ImagePictureBox.Source = bmi;

                    if (IsSupportedFormat(imageUrl))
                    {
                        // Populate the copyright message, not that this will be "NASA" if no name is provided.
                        if (imageCopyright != null && imageCopyright.Length > 0)
                        {
                            ImageCopyrightTextBox.Text = imageCopyright;
                        }

                        // Populate the description text box.
                        DescriptionTextBox.Text = imageDescription;
                    }
                    else
                    {
                        DescriptionTextBox.Text = $"Image type is not supported. URL is {imageUrl}";
                    }
                }
                catch (Exception ex)
                {
                    DescriptionTextBox.Text = $"Image data is not supported. {ex.Message}";
                }

                // Keep track of our downloads, in case we reach the limit.
                ++imageCountToday;
                ImagesTodayTextBox.Text = imageCountToday.ToString();
            }
            else
            {
                DescriptionTextBox.Text = "We were unable to retrieve the NASA picture for that day: " +
                    $"{response.StatusCode.ToString()} {response.ReasonPhrase}";
            }
        }

        private void Grid_LostFocus(object sender, RoutedEventArgs e)
        {
            WriteSettings();
        }

        private void WriteSettings()
        {
            // Preserve the required UI settings in the local storage container.
            localSettings.Values[SettingDateToday] = DateTime.Today.ToString();
            localSettings.Values[SettingShowOnStartup] = ShowTodaysImageCheckBox.IsChecked.ToString();
            localSettings.Values[SettingLimitRange] = LimitRangeCheckBox.IsChecked.ToString();
            localSettings.Values[SettingImageCountToday] = imageCountToday.ToString();
        }

        private void ReadSettings()
        {
            // If the app is being started the same day that it was run previously, then the images downloaded today count
            // needs to be set to the stored setting. Otherwise it should be zero.
            bool isToday = false;
            Object todayObject = localSettings.Values[SettingDateToday];

            if (todayObject != null)
            {
                // First check to see if this is the same day as the previous run of the app.
                DateTime dt = DateTime.Parse((string)todayObject);
                if (dt.Equals(DateTime.Today))
                {
                    isToday = true;
                }
            }

            // Set the default for images downloaded today.
            imageCountToday = 0;

            if (isToday)
            {
                Object value = localSettings.Values[SettingImageCountToday];
                if (value != null)
                {
                    imageCountToday = int.Parse((string)value);
                }
            }
            ImagesTodayTextBox.Text = imageCountToday.ToString();

            // Set the UI checkboxes, depending on the stored settings or defaults if there are no settings.
            Object showTodayObject = localSettings.Values[SettingShowOnStartup];
            if (showTodayObject != null)
            {
                ShowTodaysImageCheckBox.IsChecked = bool.Parse((string)showTodayObject);
            }
            else
            {
                // Set the default.
                ShowTodaysImageCheckBox.IsChecked = true;
            }

            Object limitRangeObject = localSettings.Values[SettingLimitRange];
            if (limitRangeObject != null)
            {
                LimitRangeCheckBox.IsChecked = bool.Parse((string)limitRangeObject);
            }
            else
            {
                // Set the default.
                LimitRangeCheckBox.IsChecked = false;
            }

            // Show today's image if the check box requires it.
            if (ShowTodaysImageCheckBox.IsChecked == true)
            {
                MonthCalendar.Date = DateTime.Today;
            }
        }        
    }
}


