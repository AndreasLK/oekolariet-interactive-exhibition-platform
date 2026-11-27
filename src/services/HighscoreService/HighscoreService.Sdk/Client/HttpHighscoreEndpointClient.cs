using System.Net.Http.Json;
using HighscoreService.Sdk.Exceptions;
using HighscoreService.Sdk.Models.Highscores.Requests;
using HighscoreService.Sdk.Models.Highscores.Responses;
using HighscoreService.Sdk.Models.Statistics.Responses;

namespace HighscoreService.Sdk.Client
{
        /// <summary>
        /// Low-level HTTP client targeting a single Highscore Service endpoint (Hub or Screen).
        /// </summary>
        /// <param name="httpClient">Configured HTTP client.</param>
        internal sealed class HttpHighscoreEndpointClient(HttpClient httpClient)
        {
                private const string BASE_ROUTE = "api/v1/highscores";

                private readonly HttpClient _httpClient = httpClient;

                public async Task<HighscoreResponse> RecordAsync(
                        RecordHighscoreRequest request,
                        CancellationToken cancellationToken)
                {
                        using HttpResponseMessage response = await this._httpClient.PostAsJsonAsync(
                                requestUri: BASE_ROUTE,
                                value: request,
                                cancellationToken: cancellationToken);

                        await EnsureSuccessAsync(response, cancellationToken);
                        HighscoreResponse? result = await response.Content.ReadFromJsonAsync<HighscoreResponse>(
                                cancellationToken: cancellationToken);

                        return result ?? throw new HighscoreServiceApiException(
                                statusCode: response.StatusCode,
                                problemDetails: null,
                                message: "Highscore API returned an empty response body.");
                }

                public async Task<PagedHighscoreResponse> QueryAsync(
                        HighscoreQueryRequest query,
                        CancellationToken cancellationToken)
                {
                        using HttpResponseMessage response = await this._httpClient.PostAsJsonAsync(
                                requestUri: $"{BASE_ROUTE}/query",
                                value: query,
                                cancellationToken: cancellationToken);

                        await EnsureSuccessAsync(response, cancellationToken);
                        PagedHighscoreResponse? result = await response.Content.ReadFromJsonAsync<PagedHighscoreResponse>(
                                cancellationToken: cancellationToken);

                        return result ?? new PagedHighscoreResponse(
                                Items: [],
                                TotalCount: 0,
                                Page: query.Page,
                                PageSize: query.PageSize);
                }

                public async Task<HighscoreStatisticsResponse> GetStatisticsAsync(
                        HighscoreQueryRequest query,
                        CancellationToken cancellationToken)
                {
                        using HttpResponseMessage response = await this._httpClient.PostAsJsonAsync(
                                requestUri: $"{BASE_ROUTE}/statistics",
                                value: query,
                                cancellationToken: cancellationToken);

                        await EnsureSuccessAsync(response, cancellationToken);
                        HighscoreStatisticsResponse? result = await response.Content.ReadFromJsonAsync<HighscoreStatisticsResponse>(
                                cancellationToken: cancellationToken);

                        return result ?? throw new HighscoreServiceApiException(
                                statusCode: response.StatusCode,
                                problemDetails: null,
                                message: "Statistics API returned an empty response body.");
                }

                public async Task DeleteByIdAsync(
                        Guid id,
                        CancellationToken cancellationToken)
                {
                        using HttpResponseMessage response = await this._httpClient.DeleteAsync(
                                requestUri: $"{BASE_ROUTE}/{id}",
                                cancellationToken: cancellationToken);

                        await EnsureSuccessAsync(response, cancellationToken);
                }

                public async Task DeleteByGameIdAsync(
                        Guid gameId,
                        CancellationToken cancellationToken)
                {
                        using HttpResponseMessage response = await this._httpClient.DeleteAsync(
                                requestUri: $"{BASE_ROUTE}/games/{gameId}",
                                cancellationToken: cancellationToken);

                        await EnsureSuccessAsync(response, cancellationToken);
                }

                private static async Task EnsureSuccessAsync(
                        HttpResponseMessage response,
                        CancellationToken cancellationToken)
                {
                        if (response.IsSuccessStatusCode)
                        {
                                return;
                        }

                        HighscoreProblemDetails? problem = null;
                        try
                        {
                                problem = await response.Content.ReadFromJsonAsync<HighscoreProblemDetails>(
                                        cancellationToken: cancellationToken);
                        }
                        catch
                        {
                                // Ignore non-JSON error bodies and surface the HTTP status code.
                        }

                        string detail = problem?.Detail ?? problem?.Title ?? response.ReasonPhrase ?? "Request failed.";
                        throw new HighscoreServiceApiException(
                                statusCode: response.StatusCode,
                                problemDetails: problem,
                                message: $"HighscoreService request failed ({(int)response.StatusCode}): {detail}");
                }
        }
}

