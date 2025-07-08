using JsonConfigEditor.Core.Settings;  
using System;  
using System.IO;  
using System.Text.Json;  
using System.Threading.Tasks;

namespace JsonConfigEditor.Core.Services  
{  
    public class UserSettingsService  
    {  
        private readonly string _settingsFilePath;  
        private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

        public UserSettingsService()  
        {  
            // Get the path to the user's local app data folder  
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);  
            var settingsDir = Path.Combine(appDataPath, "JsonConfigEditor");  
            Directory.CreateDirectory(settingsDir); // Ensure the directory exists  
            _settingsFilePath = Path.Combine(settingsDir, "settings.json");  
        }

        /// <summary>  
        /// Asynchronously loads user settings from the file system.  
        /// </summary>  
        /// <returns>The loaded UserSettingsModel, or a new instance with defaults if the file doesn't exist.</returns>  
        public async Task<UserSettingsModel> LoadSettingsAsync()  
        {  
            if (!File.Exists(_settingsFilePath))  
            {  
                return new UserSettingsModel(); // Return defaults  
            }

            try  
            {  
                var json = await File.ReadAllTextAsync(_settingsFilePath);  
                return JsonSerializer.Deserialize<UserSettingsModel>(json) ?? new UserSettingsModel();  
            }  
            catch (Exception ex)  
            {  
                // Log the error and return defaults  
                Console.Error.WriteLine($"Error loading user settings: {ex.Message}");  
                return new UserSettingsModel();  
            }  
        }

        /// <summary>  
        /// Asynchronously saves the provided user settings model to the file system.  
        /// </summary>  
        public async Task SaveSettingsAsync(UserSettingsModel settings)  
        {  
            try  
            {  
                var json = JsonSerializer.Serialize(settings, _serializerOptions);  
                await File.WriteAllTextAsync(_settingsFilePath, json);  
            }  
            catch (Exception ex)  
            {  
                // Log the error  
                Console.Error.WriteLine($"Error saving user settings: {ex.Message}");  
            }  
        }  
    }  
} 