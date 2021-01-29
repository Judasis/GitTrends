﻿using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using GitTrends.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Pipeline;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GitTrends.Functions
{
    class GenerateGitHubOAuthToken
    {
        readonly static string _clientSecret = Environment.GetEnvironmentVariable("GitTrendsClientSecret") ?? string.Empty;
        readonly static string _clientId = Environment.GetEnvironmentVariable("GitTrendsClientId") ?? string.Empty;

        readonly GitHubAuthService _gitHubAuthService;

        public GenerateGitHubOAuthToken(GitHubAuthService gitHubAuthService) => _gitHubAuthService = gitHubAuthService;

        [FunctionName(nameof(GenerateGitHubOAuthToken))]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, FunctionExecutionContext executionContext)
        {
            var logger = executionContext.Logger;
            logger.LogInformation("Received request for OAuth Token");

            var generateTokenDTO = JsonConvert.DeserializeObject<GenerateTokenDTO>(req.Body);

            var token = await _gitHubAuthService.GetGitHubToken(_clientId, _clientSecret, generateTokenDTO.LoginCode, generateTokenDTO.State).ConfigureAwait(false);

            logger.LogInformation("Token Retrieved");

            return new ContentResult
            {
                Content = JsonConvert.SerializeObject(token),
                StatusCode = (int)HttpStatusCode.OK,
                ContentType = "application/json"
            };
        }
    }
}
