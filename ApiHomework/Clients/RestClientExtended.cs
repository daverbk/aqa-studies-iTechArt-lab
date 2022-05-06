using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using ApiHomework.Configuration;
using ApiHomework.Configuration.Enums;
using NLog;
using RestSharp;
using RestSharp.Authenticators;

namespace ApiHomework.Clients;

public class RestClientExtended
{
    private readonly RestClient _client;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static RestResponse LastCallResponse { get; set; } = null!;

    public RestClientExtended(UserType userType)
    {
        var options = new RestClientOptions(Configurator.AppSettings.BaseUrl ??
                                            throw new InvalidOperationException(
                                                "Base url can't be null. Check appsettings.json before the next restart."));

        _client = new RestClient(options);

        if (userType == UserType.Unauthorized)
        {
            return;
        }

        _client.Authenticator = userType == UserType.Admin
            ? new HttpBasicAuthenticator(Configurator.Admin.Email, Configurator.Admin.Password)
            : new HttpBasicAuthenticator(Configurator.User.Email, Configurator.User.Password);
    }

    public async Task<T> ExecuteAsync<T>(RestRequest request)
    {
        LogRequest(request);
        var response = await _client.ExecuteAsync<T>(request);
        LogResponse(response);

        UpdateLastCallResponse(response);

        return response.Data ??
               throw new SerializationException(
                   "Response deserialization error. Debug with breakpoints on model's properties for more information.",
                   response.ErrorException);
    }

    public async Task<RestResponse> ExecuteAsync(RestRequest request)
    {
        LogRequest(request);
        var response = await _client.ExecuteAsync(request);
        LogResponse(response);

        return response;
    }

    private void LogRequest(RestRequest request)
    {
        _logger.Debug($"{request.Method} request to: {request.Resource}");

        var body = request.Parameters
            .FirstOrDefault(p => p.Type == ParameterType.RequestBody)?.Value;

        if (body != null)
        {
            _logger.Debug($"body : {body}");
        }
    }

    private void LogResponse(RestResponse response)
    {
        if (response.ErrorException != null)
        {
            _logger.Error(
                $"Error retrieving response. Check inner details for more info. \n{response.ErrorException.Message}");
        }

        _logger.Debug($"Request finished with status code : {response.StatusCode}");

        if (!string.IsNullOrEmpty(response.Content))
        {
            _logger.Debug(response.Content);
        }
    }

    private void UpdateLastCallResponse(RestResponse lastCallResponse)
    {
        LastCallResponse = lastCallResponse;
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
