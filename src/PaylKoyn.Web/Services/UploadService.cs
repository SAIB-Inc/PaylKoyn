using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using PaylKoyn.Data.Requests;
using PaylKoyn.Data.Responses;

namespace PaylKoyn.Web.Services;

public class UploadService(HttpClient httpClient, IConfiguration configuration)
{
    private readonly string _apiBaseUrl = configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5246/api/v1";

    public async Task<EstimateFeeResponse?> EstimateFeeAsync(long fileSize)
    {
        var request = new EstimateFeeRequest((int)fileSize);
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{_apiBaseUrl}/upload/estimate-fee", content);
        
        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<EstimateFeeResponse>(jsonResponse, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }

        return null;
    }

    public async Task<UploadRequestResponse?> RequestUploadAsync()
    {
        var response = await httpClient.PostAsync($"{_apiBaseUrl}/upload/request", null);
        
        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UploadRequestResponse>(jsonResponse, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }

        return null;
    }

    public async Task<(bool IsSuccess, string Message)> UploadFileAsync(string uploadId, IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        
        content.Add(new StringContent(uploadId), "Id");
        content.Add(new StringContent(file.Name), "Name");
        content.Add(new StringContent(file.ContentType), "ContentType");
        
        // Read file into memory first to avoid Blazor Server stream issues
        using var fileStream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024); // 100MB max
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        
        var fileBytes = memoryStream.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "File", file.Name);

        var response = await httpClient.PostAsync($"{_apiBaseUrl}/upload/receive", content);
        var message = await response.Content.ReadAsStringAsync();
        
        return (response.IsSuccessStatusCode, message);
    }
}