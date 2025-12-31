using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PavanamDroneConfigurator.Core.Interfaces;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class PersistenceService : IPersistenceService
{
    private readonly ILogger<PersistenceService> _logger;
    private readonly string _profilesPath;

    public PersistenceService(ILogger<PersistenceService> logger)
    {
        _logger = logger;
        _profilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PavanamDroneConfigurator",
            "Profiles");

        Directory.CreateDirectory(_profilesPath);
    }

    public async Task<bool> SaveProfileAsync(string profileName, Dictionary<string, object> data)
    {
        try
        {
            var filePath = Path.Combine(_profilesPath, $"{profileName}.json");
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);

            await File.WriteAllTextAsync(filePath, json);

            _logger.LogInformation("Profile '{Profile}' saved successfully", profileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving profile '{Profile}'", profileName);
            return false;
        }
    }

    public async Task<Dictionary<string, object>?> LoadProfileAsync(string profileName)
    {
        try
        {
            var filePath = Path.Combine(_profilesPath, $"{profileName}.json");

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Profile '{Profile}' not found", profileName);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            _logger.LogInformation("Profile '{Profile}' loaded successfully", profileName);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profile '{Profile}'", profileName);
            return null;
        }
    }

    public Task<List<string>> GetProfileNamesAsync()
    {
        try
        {
            var files = Directory.GetFiles(_profilesPath, "*.json");
            var profiles = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();

            _logger.LogInformation("Found {Count} profiles", profiles.Count);
            return Task.FromResult(profiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile names");
            return Task.FromResult(new List<string>());
        }
    }
}
