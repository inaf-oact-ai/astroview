using Newtonsoft.Json;

namespace AstroView.WebApp.App.Integrations.CaesarApi;

public class CaesarApiClient
{
    public const string APP_UMAP = "umap";
    public const string APP_HDBSCAN = "hdbscan";
    public const string APP_OUTLIER_FINDER = "outlier-finder";
    public const string APP_SIMILARITY_SEARCH = "similarity-search";
    public const string APP_CLASSIFIER_VIT = "classifier-vit";

    private readonly HttpClient client;

    public CaesarApiClient(HttpClient client, string baseAddress)
    {
        this.client = client;
        this.client.BaseAddress = new Uri(baseAddress);
    }

    public async Task<string> Describe(string app)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"app/{app}/describe");
        var res = await client.SendAsync(req);
        await EnsureSuccessStatusCode(res);
        var resBody = await res.Content.ReadAsStringAsync();
        return resBody;
    }

    public async Task<SubmitJobResponse> SubmitJob(SubmitJobRequest request)
    {
        var reqJson = JsonConvert.SerializeObject(request);
        var req = new HttpRequestMessage(HttpMethod.Post, "job");
        req.Content = new StringContent(reqJson, null, "application/json");
        var res = await client.SendAsync(req);
        await EnsureSuccessStatusCode(res);
        var resBody = await res.Content.ReadAsStringAsync();
        var job = JsonConvert.DeserializeObject<SubmitJobResponse>(resBody)!;
        return job;
    }

    public async Task<JobStatusResponse> GetJobStatus(string jobId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"job/{jobId}/status");
        var res = await client.SendAsync(req);
        await EnsureSuccessStatusCode(res);
        var resBody = await res.Content.ReadAsStringAsync();
        var jobStatus = JsonConvert.DeserializeObject<JobStatusResponse>(resBody)!;
        return jobStatus;
    }

    public async Task DownloadJobOutput(string jobId, string outputFile)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"job/{jobId}/output");
        var res = await client.SendAsync(req);
        await EnsureSuccessStatusCode(res);
        using var stream = await res.Content.ReadAsStreamAsync();
        using var fs = File.Create(outputFile);
        stream.CopyTo(fs);
    }

    public async Task<UploadFileResponse> UploadFile(UploadFileRequest request)
    {
        var filename = Path.GetFileName(request.FilePath);
        var multiForm = new MultipartFormDataContent();
        FileStream fs = File.OpenRead(request.FilePath);
        multiForm.Add(new StreamContent(fs), "file", filename);

        var req = new HttpRequestMessage(HttpMethod.Post, "upload");
        req.Content = multiForm;
        var res = await client.SendAsync(req);
        await EnsureSuccessStatusCode(res);
        var resBody = await res.Content.ReadAsStringAsync();
        var job = JsonConvert.DeserializeObject<UploadFileResponse>(resBody)!;
        return job;
    }

    private static async Task EnsureSuccessStatusCode(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
        {
            string text = "N/A";
            try
            {
                text = await res.Content.ReadAsStringAsync();
            }
            catch
            {
            }
            throw new Exception($"Failed to call Caesar API. Response status code: {(int)res.StatusCode}. Response text: {text}.");
        }
    }

    public class ParameterDescription
    {
        public int advanced { get; set; }
        public string category { get; set; } = null!;
        public object @default { get; set; } = null!;
        public string description { get; set; } = null!;
        public bool @enum { get; set; }
        public bool mandatory { get; set; }
        public object max { get; set; } = null!;
        public object min { get; set; } = null!;
        public string subcategory { get; set; } = null!;
        public string type { get; set; } = null!;
    }

    public class SubmitJobResponse
    {
        public string app { get; set; } = null!;
        public object data_inputs { get; set; } = null!; // can be `string` or `List<string>`
        public string job_id { get; set; } = null!;
        public Dictionary<string, object> job_options { get; set; } = null!;
        public string state { get; set; } = null!;
        public string status { get; set; } = null!;
        public DateTime submit_date { get; set; }
        public string tag { get; set; } = null!;
    }

    public class JobStatusResponse
    {
        public string elapsed_time { get; set; } = null!;
        public int exit_status { get; set; }
        public string job_id { get; set; } = null!;
        public string pid { get; set; } = null!;
        public string state { get; set; } = null!;
        public string status { get; set; } = null!;
    }

    public class SubmitJobRequest
    {
        public string app { get; set; } = null!;
        public DataInputs data_inputs { get; set; }
        public Dictionary<string, object> job_options { get; set; }

        public SubmitJobRequest()
        {
            data_inputs = new DataInputs();
            job_options = new Dictionary<string, object>();
        }

        public class DataInputs
        {
            public object data { get; set; } = null!; // can be `string` or `List<string>`
            public object format { get; set; } = null!; // can be `string` or `List<string>`
        }
    }

    public class UploadFileRequest
    {
        public string FilePath { get; set; } = null!;
    }

    public class UploadFileResponse
    {
        public string uuid { get; set; } = null!;
    }
}
