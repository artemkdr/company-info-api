Let's build an API service for getting info about companies, using available APIs (vies, CH IDE API, societe.com, insee.fr, etc.).

We already use these APIs in our internal tools, but we want to create a public API that can be used by other developers.

## For VAT checking we use this:
``` C#
Padi.Vies.ViesManager viesManager = new Padi.Vies.ViesManager();
var result = Padi.Vies.ViesManager.IsValid(q).IsValid; 
var isActiveVat = viesManager.IsActive(q).Result.IsValid;
```
It works, but it returns MS_MAX_CONCURRENT_REQ quite often. We can leave with it, we have just to handle this error and retry after some time, but it would be better to have a more reliable solution.

## For CH IDE API we use this:
It works.
``` C#
try {
    string cleanedString = new string(PageContext.Request.GetValue("getinfo").Where(char.IsDigit).ToArray());
    // Convert the cleaned string to an integer
    int id;
    int.TryParse(cleanedString, out id);
    rslt = PostSoap(
        "https://www.uid-wse.admin.ch/V5.0/PublicServices.svc", 
        "http://www.uid.admin.ch/xmlns/uid-wse/IPublicServices/GetByUID",
        "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:uid=\"http://www.uid.admin.ch/xmlns/uid-wse\" xmlns:ns=\"http://www.ech.ch/xmlns/eCH-0097/5\"><soapenv:Header/><soapenv:Body><uid:GetByUID><uid:uid><ns:uidOrganisationIdCategorie>CHE</ns:uidOrganisationIdCategorie><ns:uidOrganisationId>" + id + "</ns:uidOrganisationId></uid:uid></uid:GetByUID></soapenv:Body></soapenv:Envelope>"			
    );				
    //var bag = XmlSer.Deserialize<Bag>(rslt);
    //Console.WriteLine(rslt);
} catch {
    rslt = "<Result status=\"error\" ide=\"" + id + "\" />";
}
```
and
``` C#
var q = PageContext.Request.GetValue("id") ?? PageContext.Request.GetValue("num") ?? "";	
var rslt = PostSoap(
    "https://www.uid-wse.admin.ch/V5.0/PublicServices.svc", 
    "http://www.uid.admin.ch/xmlns/uid-wse/IPublicServices/ValidateUID",
    "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:uid=\"http://www.uid.admin.ch/xmlns/uid-wse\"><soapenv:Header/><soapenv:Body><uid:ValidateUID><uid:uid>" + q  + "</uid:uid></uid:ValidateUID></soapenv:Body></soapenv:Envelope>"				
);					
if (rslt.ContainsMatch("<ValidateUIDResult>true</ValidateUIDResult>")) {
    isActive = true;
} else {
    isActive = false;			
}
```

## For Insee API we use this:
``` C#
try {
    var rsp = HttpUtil.Get("https://api.insee.fr/api-sirene/3.11/siret/" + regNum, "e544d7fd-6301-404c-84d7-fd6301304c07", "X-INSEE-Api-Key-Integration");
    
    if (rsp != null) {
        var apiRslt = JSer.Deserialize<Bag>(rsp);        			
        var establishment = apiRslt.Get<Bag>("etablissement") ?? new Bag();
        var estAddress = establishment.Get<Bag>("adresseEtablissement") ?? new Bag();
        var legalUnit = establishment.Get<Bag>("uniteLegale") ?? new Bag();        			

        rslt = new Bag();
        var apen = legalUnit.GetString("activitePrincipaleUniteLegale") ?? "";        			
        if (legalUnit.GetString("etatAdministratifUniteLegale") == "A" && apen.StartsWith("45")) {
            var periods = establishment.Get<List<Bag>>("periodesEtablissement") ?? new List<Bag>();
            rslt.Add("ENSEIGNE", periods.Count >= 1 ? periods.First().GetString("enseigne1Etablissement") : "");
            rslt.Add("NOMEN_LONG", legalUnit.GetString("denominationUniteLegale"));
            rslt.Add("L1_NORMALISEE", legalUnit.GetString("denominationUniteLegale"));
            rslt.Add("APEN700", apen);
            rslt.Add("NOM", legalUnit.GetString("nomUniteLegale"));
            rslt.Add("PRENOM", legalUnit.GetString("prenom1UniteLegale"));
            rslt.Add("CODPOS", estAddress.GetString("codePostalEtablissement"));
            rslt.Add("LIBCOM", estAddress.GetString("libelleCommuneEtablissement"));        			
        }
    }
} catch {        		
}
```

## Bodacc API:
We don't have a code snippet for this, but we can use the Bodacc API to get information about companies that have been declared bankrupt or have been dissolved. We can use this information to filter out companies that are no longer active.
We just need to search by SIREN/SIRET number and to get any info if the company is in liquidation or not.
https://help.opendatasoft.com/apis/ods-explore-v2/#tag/Dataset/operation/getRecords
https://help.opendatasoft.com/apis/ods-explore-v2/explore_v2.1.html


## Also we need an endpoint to generate a TVA number from a SIREN number, for France. We can use this:
``` C#
string CalculateTVANumber(string siren) {    	
    siren = siren.Substring(0, Math.Min(9, siren.Length));
    int sirenInt = 0;
    if (Int32.TryParse(siren, out sirenInt)) {				
        return "FR" + ((12 + 3 * (sirenInt % 97)) % 97).ToString().PadLeft(2, '0') + siren;
    }
    return null;
} 
```


Use .net 9+ and Memory caching for caching results from the APIs to improve performance and reduce the number of requests to the external APIs. 
We can set an expiration time for the cache to ensure that we are getting up-to-date information (via appsettings.json).
Wrap all these functionalities in separate Feature under Application folder.
Every endpoint must be capable to respond in JSON or XML format, based on url parameter (e.g. ?format=json or ?format=xml).
All the request/response models must be defined in Feature/../Models folder with suffix Request/Response, e.g. GetCompanyInfoRequest, GetCompanyInfoResponse, etc. We can use common model if needed for the requests, because e.g. we need to include format parameter in all the requests.
Use API key authentication for our API.
Keep all api keys for other public APIs in a secure configuration file, and do not hardcode them in the codebase. Use `dotnet user-secrets set` to store sensitive information during development and consider using environment variables for production.
Write units tests for all the functionalities to ensure that they are working as expected and to catch any potential bugs early on.
Use existing archictecture tests to test the architecture.
Create *.http files for testing the API endpoints with VS Code REST Client. Put them in docs folder.
Create a README.md file with instructions on how to set up and use the API, including how to obtain API keys for the external APIs and how to run the unit tests.
Create a CONFIGURATION.md file with details about the configuration options available in appsettings.json, including cache expiration times and API keys, and tools needed to build this API (husky, csharpier).
Use 'Shared' folder in src for Swagger configuration, Authentication configuration, middleware, etc. that can be shared across different features and application itself.
