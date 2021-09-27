using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CoxAuto
{
  class Program
  {
    const string API_URL = "http://api.coxauto-interview.com/api/";

    public class DatasetIdObj
    {
      public string datasetId { get; set; }
    }

    public class VehicleIdsObj
    {
      public List<int> vehicleIds { get; set; }
    }

    public class VehicleInformationObj
    {
      public int vehicleId { get; set; }
      public int year { get; set; }
      public string make { get; set; }
      public string model { get; set; }
      public int dealerId { get; set; }
    }

    public class DealerInformationObj
    {
      public int dealerId { get; set; }
      public string name { get; set; }
    }

    public class VehicleOutputObj
    {
      public int vehicleId { get; set; }
      public int year { get; set; }
      public string make { get; set; }
      public string model { get; set; }
    }

    public class DealerOutputObj
    {
      public int dealerId { get; set; }
      public string name { get; set; }
      public List<VehicleOutputObj> vehicles { get; set; }
    }

    public class OutputObj
    {
      public List<DealerOutputObj> dealers { get; set; }
    }

    static void Main()
    {
      Task t = new Task(TestApi);
      t.Start();
      Console.WriteLine("Testing API...");
      Console.ReadLine();
    }

    static async void TestApi()
    {
      string datasetId = string.Empty;

      try
      {
        datasetId = await GetDatasetId();

        List<int> vehicleIdList = await GetVehicleIdsForDatasetId(datasetId);

        List<VehicleInformationObj> vehicleInformation = await GetVehicleInformation(datasetId, vehicleIdList);

        List<int> uniqueDealerIds = vehicleInformation.Select( n => n.dealerId ).Distinct().ToList();
        List<DealerInformationObj> dealerInformation = await GetDealerInformation(datasetId, uniqueDealerIds);

        string jsonOutput = BuildOutput(dealerInformation, vehicleInformation);
        //Console.WriteLine(jsonOutput);

        string postResult = await PostAnswer(datasetId, jsonOutput);
        Console.WriteLine(postResult);
      }
      catch(Exception e)
      {
        // Write to DB or error log instead to be able to check out errors.
        Console.WriteLine("Error: {0} - Processing dataset {1}", e.Message, datasetId);
      }
    }

    static async Task<string> PostAnswer(string datasetId, string jsonOutput)
    {
      string apiUrl = API_URL + datasetId + "/answer";
      string result = string.Empty;

      using (HttpClient httpClient = new HttpClient())
      {
        StringContent data = new StringContent(jsonOutput, Encoding.UTF8, "application/json");
        using ( HttpResponseMessage response = await httpClient.PostAsync(apiUrl, data) )
        {
          result = response.Content.ReadAsStringAsync().Result;
        }
      }

      return result;
    }

    static string BuildOutput(List<DealerInformationObj> dealerInformation, List<VehicleInformationObj> vehicleInformation)
    {
      OutputObj outputObj = new OutputObj();
      outputObj.dealers = new List<DealerOutputObj>();
      foreach ( DealerInformationObj dealer in dealerInformation )
      {
        DealerOutputObj dealerOutputObj = new DealerOutputObj();
        dealerOutputObj.dealerId = dealer.dealerId;
        dealerOutputObj.name = dealer.name;
        dealerOutputObj.vehicles = vehicleInformation
            .Where( n => n.dealerId == dealer.dealerId )
            .Select( n => new VehicleOutputObj { vehicleId = n.vehicleId, year = n.year, make = n.make, model = n.model })
            .ToList();
        outputObj.dealers.Add(dealerOutputObj);
      }

      //string output = JsonConvert.SerializeObject(outputObj, Formatting.Indented);
      string output = JsonConvert.SerializeObject(outputObj);
      return output;
    }

    static async Task<string> GetDatasetId()
    {
      string apiUrl = API_URL + "datasetId";
      string retValue = string.Empty;

      using (HttpClient httpClient = new HttpClient())
      {
        using (HttpResponseMessage response = await httpClient.GetAsync(apiUrl))
        {
          string result = await response.Content.ReadAsStringAsync();

          if ( result != null )
          {
            DatasetIdObj record = JsonConvert.DeserializeObject<DatasetIdObj>(result);
            retValue = record.datasetId;
          }
        }
      }

      return retValue;
    }

    static async Task<List<int>> GetVehicleIdsForDatasetId(string datasetId)
    {
      string apiUrl = API_URL + datasetId + "/vehicles";
      List<int> retValue = null;

      using ( HttpClient httpClient = new HttpClient() )
      {
        using ( HttpResponseMessage response = await httpClient.GetAsync(apiUrl) )
        {
          string result = await response.Content.ReadAsStringAsync();

          if ( result != null )
          {
            VehicleIdsObj record = JsonConvert.DeserializeObject<VehicleIdsObj>(result);
            retValue = record.vehicleIds;
          }
        }
      }
      return retValue;
    }

    // If parameter vehicleIdList is empty, then this method will return an empty List<VehicleInformationObj>
    static async Task<List<VehicleInformationObj>> GetVehicleInformation(string datasetId, List<int> vehicleIdList)
    {
      List<VehicleInformationObj> vehicleInformation = new List<VehicleInformationObj>();

      using (HttpClient httpClient = new HttpClient())
      {
        // Build the requests.
        List<Task<HttpResponseMessage>> requestList = new List<Task<HttpResponseMessage>>();
        string apiUrl;
        foreach ( int vehicleId in vehicleIdList )
        {
          apiUrl = API_URL + datasetId + "/vehicles/" + vehicleId.ToString();
          requestList.Add(httpClient.GetAsync(apiUrl));
        }
        await Task.WhenAll(requestList);

        // Get the responses.
        foreach ( Task<HttpResponseMessage> request in requestList )
        {
          HttpResponseMessage response = request.Result;
          string result = await response.Content.ReadAsStringAsync();
          vehicleInformation.Add( JsonConvert.DeserializeObject<VehicleInformationObj>(result) );
        }
      }

      return vehicleInformation;
    }

    // If parameter dealerIdList is empty, then this method will return an empty List<DealerInformationObj>
    static async Task<List<DealerInformationObj>> GetDealerInformation(string datasetId, List<int> dealerIdList)
    {
      List<DealerInformationObj> dealerInformation = new List<DealerInformationObj>();

      using (HttpClient httpClient = new HttpClient())
      {
        // Build the requests.
        List<Task<HttpResponseMessage>> requestList = new List<Task<HttpResponseMessage>>();
        string apiUrl;
        foreach ( int dealerId in dealerIdList )
        {
          apiUrl = API_URL + datasetId + "/dealers/" + dealerId.ToString();
          requestList.Add(httpClient.GetAsync(apiUrl));
        }
        await Task.WhenAll(requestList);

        // Get the responses.
        foreach ( Task<HttpResponseMessage> request in requestList )
        {
          HttpResponseMessage response = request.Result;
          string result = await response.Content.ReadAsStringAsync();
          dealerInformation.Add(JsonConvert.DeserializeObject<DealerInformationObj>(result));
        }
      }

      return dealerInformation;
    }

  }
}
