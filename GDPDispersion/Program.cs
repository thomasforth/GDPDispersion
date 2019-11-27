using CsvHelper;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GDPDispersion
{
    class Program
    {
        static void Main(string[] args)
        {
            List<GDPTotalRaw> RawGDPTotals;
            List<GDPPerHeadRaw> RawGDPPerHead;
            List<IncomeRaw> RawIncomes;

            // Load the three files
            /*
            using (TextReader textReader = File.OpenText(@"Assets\nama_10r_2gdp_1_Data.csv"))
            {
                CsvReader csvReader = new CsvReader(textReader);
                RawGDPTotals = new List<GDPTotalRaw>(csvReader.GetRecords<GDPTotalRaw>());
            }
            */

            using (TextReader textReader = File.OpenText(@"Assets\nama_10r_2gdp_2_Data.csv"))
            {
                CsvReader csvReader = new CsvReader(textReader);
                RawGDPPerHead = new List<GDPPerHeadRaw>(csvReader.GetRecords<GDPPerHeadRaw>());
            }

            using (TextReader textReader = File.OpenText(@"Assets\nama_10r_2hhinc_1_Data.csv"))
            {
                CsvReader csvReader = new CsvReader(textReader);
                RawIncomes = new List<IncomeRaw>(csvReader.GetRecords<IncomeRaw>());
            }

            // Create populations list
            List<Population> Populations = new List<Population>();
            using (TextReader textReader = File.OpenText("Assets/demo_r_d2jan_1_Data.csv"))
            {
                CsvReader csvReader = new CsvReader(textReader);
                csvReader.Configuration.HeaderValidated = null;
                csvReader.Configuration.MissingFieldFound = null;
                Populations = csvReader.GetRecords<Population>().ToList();
            }

            // Load historic GDP/head (at PPS) data.
            List<HistoricGDPPerHeadPPS> HistoricGDPs = new List<HistoricGDPPerHeadPPS>();
            using (TextReader textReader = File.OpenText("Assets/euregionsabsolutepps_unpivotted.csv"))
            {
                CsvReader csvReader = new CsvReader(textReader);
                HistoricGDPs = csvReader.GetRecords<HistoricGDPPerHeadPPS>().ToList();
            }

            List<CombinedData> HistoricCombinedData = new List<CombinedData>();
            foreach(HistoricGDPPerHeadPPS historicGDPPerHeadPPS in HistoricGDPs)
            {
                CombinedData combinedData = new CombinedData();
                combinedData.CountryOrGrouping = historicGDPPerHeadPPS.regionName;
                combinedData.NUTSLevel = historicGDPPerHeadPPS.NUTSlevel;
                combinedData.RegionCode = historicGDPPerHeadPPS.NUTScode;
                combinedData.GDPPerHead = historicGDPPerHeadPPS.Value;
                combinedData.Year = historicGDPPerHeadPPS.Year;

                if (Populations.Where(x => x.GEO == combinedData.RegionCode && x.TIME == combinedData.Year).FirstOrDefault() != null)
                {
                    combinedData.Population = Populations.Where(x => x.GEO == combinedData.RegionCode && x.TIME == combinedData.Year).First().Value;
                }
                else if (Populations.Where(x => x.GEO == combinedData.RegionCode).FirstOrDefault() != null)
                {
                    combinedData.Population = Populations.Where(x => x.GEO == combinedData.RegionCode).First().Value;
                }
                else
                {
                    Console.WriteLine($"No population data found for {combinedData.CountryOrGrouping} in {combinedData.Year}.");
                }                
                combinedData.GDPTotal = combinedData.Population * combinedData.GDPPerHead;

                HistoricCombinedData.Add(combinedData);
            }

            List<CombinedData> CombinedCurrentData = new List<CombinedData>();
            foreach (GDPPerHeadRaw gDPPerHeadRaw in RawGDPPerHead)
            {
                int dummyint = 0;
                if (int.TryParse(gDPPerHeadRaw.Value.Replace(",", ""), out dummyint))
                {
                    // Create combined data object and start by adding GDP per Head (PPP)
                    CombinedData combinedData = new CombinedData()
                    {
                        Year = gDPPerHeadRaw.TIME,
                        RegionCode = gDPPerHeadRaw.GEO,
                        CountryOrGrouping = gDPPerHeadRaw.GEO_LABEL,
                        GDPPerHead = int.Parse(gDPPerHeadRaw.Value.Replace(",", "")),
                    };
                    if (gDPPerHeadRaw.GEO.StartsWith("EU") == false)
                    {
                        combinedData.NUTSLevel = gDPPerHeadRaw.GEO.Length - 2;
                    }

                    // Add GDP (PPP) Total and calculate population
                    if (Populations.Where(x => x.GEO == combinedData.RegionCode && x.TIME == combinedData.Year).FirstOrDefault() != null)
                    {
                        combinedData.Population = Populations.Where(x => x.GEO == combinedData.RegionCode && x.TIME == combinedData.Year).First().Value;
                    }
                    else if (Populations.Where(x => x.GEO == combinedData.RegionCode).FirstOrDefault() != null)
                    {
                        combinedData.Population = Populations.Where(x => x.GEO == combinedData.RegionCode).First().Value;
                    }
                    else
                    {
                        Console.WriteLine($"No population data found for {combinedData.CountryOrGrouping} in {combinedData.Year}.");
                    }
                    combinedData.GDPTotal = combinedData.Population * combinedData.GDPPerHead;

                    // Add Income (PPP) per head
                    if (RawIncomes.Where(x => x.TIME == combinedData.Year && x.GEO == combinedData.RegionCode).FirstOrDefault() != null &&
                        int.TryParse(RawIncomes.Where(x => x.TIME == combinedData.Year && x.GEO == combinedData.RegionCode).FirstOrDefault().Value.Replace(",", ""), out dummyint))
                    {
                        combinedData.IncomePerHead = int.Parse(RawIncomes.Where(x => x.TIME == combinedData.Year && x.GEO == combinedData.RegionCode).FirstOrDefault().Value.Replace(",", ""));
                    }

                    // Calculate Income total
                    combinedData.IncomeTotal = combinedData.IncomePerHead * combinedData.Population;
                    CombinedCurrentData.Add(combinedData);
                }
            }

            // Calculate dispersions for both historic and current datasets
            List<DispersionOutput> HistoricDispersionOutputs = CalculateDispersions(HistoricCombinedData);
            List<DispersionOutput> CurrentDispersionOutputs = CalculateDispersions(CombinedCurrentData);


            // Merge the two outputs (prefering the current dataset results)
            List<DispersionOutput> CombinedDispersionOutputs = new List<DispersionOutput>();
            CombinedDispersionOutputs.AddRange(HistoricDispersionOutputs);
            foreach(DispersionOutput currentDispersionOutput in CurrentDispersionOutputs)
            {
                if (currentDispersionOutput.Country.StartsWith("EU") || currentDispersionOutput.Country.StartsWith("EA"))
                {
                    if (currentDispersionOutput.Year >= 2015)
                    {
                        CombinedDispersionOutputs.RemoveAll(x => x.Country == currentDispersionOutput.Country && x.Year == currentDispersionOutput.Year);
                        CombinedDispersionOutputs.Add(currentDispersionOutput);
                    }
                }
                else
                {
                    if (double.IsNaN(currentDispersionOutput.GDPDispersion) == false && CombinedDispersionOutputs.Exists(x => x.Country == currentDispersionOutput.Country && x.Year == currentDispersionOutput.Year))
                    {
                        CombinedDispersionOutputs.RemoveAll(x => x.Country == currentDispersionOutput.Country && x.Year == currentDispersionOutput.Year);
                        CombinedDispersionOutputs.Add(currentDispersionOutput);
                    }
                    if (CombinedDispersionOutputs.Exists(x => x.Country == currentDispersionOutput.Country && x.Year == currentDispersionOutput.Year) == false)
                    {
                        CombinedDispersionOutputs.Add(currentDispersionOutput);
                    }
                }
            }

            // Output the results
            using (TextWriter writer = new StreamWriter($"CalculatedDisperions_LondonFixed.csv", false, System.Text.Encoding.UTF8))
            {
                var csv = new CsvWriter(writer);
                csv.WriteRecords(CombinedDispersionOutputs);
            }
        }

        static List<DispersionOutput> CalculateDispersions(List<CombinedData> CombinedData)
        {
            // All the data we need is now loaded in. Time to calculate Theil Index and GDP dispersion
            // https://en.wikipedia.org/wiki/Theil_index
            // 

            List<DispersionOutput> DispersionOutputs = new List<DispersionOutput>();

            List<string> UniqueCountries = CombinedData.Select(x => x.RegionCode.Substring(0, 2)).Distinct().ToList();
            UniqueCountries.Remove("EU");
            UniqueCountries.Add("EA12");
            UniqueCountries.Add("EU15");
            UniqueCountries.Add("EU19");
            UniqueCountries.Add("EU27");
            UniqueCountries.Add("UK*");

            List<int> UniqueYears = CombinedData.Select(x => x.Year).Distinct().ToList();
            foreach (string country in UniqueCountries)
            {
                string loopcountry = country;
                List<CombinedData> NUTS2RegionsInTheCountry = CombinedData.Where(x => x.RegionCode.StartsWith(country) && x.NUTSLevel == 2).ToList();

                // whole-EU calculations are a special case. The LondonFix is applied and all NUTS2 regions for nations within the EU are used.
                // CURRENTLY DOES NOT INCLUDE CROATIA (HR). THIS MAY NEED FIXING SOON!
                List<string> EA12Countries = new List<string>() { "AT", "BE", "DE", "EL", "ES", "FI", "FR", "IE", "IT", "LU", "NL", "PT"};
                List<string> EU15Countries = new List<string>() { "AT", "BE", "DE", "DK", "EL", "ES", "FI", "FR", "IE", "IT", "LU", "NL", "PT", "SE", "UK" };
                List<string> EU19Countries = new List<string>() { "AT", "BE", "DE", "DK", "EL", "ES", "FI", "FR", "IE", "IT", "LU", "NL", "PT", "SE", "UK", "SK", "PL", "HU", "CZ" };
                List<string> EU27Countries = new List<string>() { "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "EL", "ES", "FI", "FR", "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK", "UK" };

                // IRELAND is REMOVED BECAUSE ITS GDP DATA IS USELESS
                EA12Countries.Remove("IE");
                EU15Countries.Remove("IE");
                EU19Countries.Remove("IE");
                EU27Countries.Remove("IE");

                if (country == "EA12")
                {
                    NUTS2RegionsInTheCountry.Clear();
                    foreach (string EUcountry in EA12Countries)
                    {
                        NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode.StartsWith(EUcountry) && x.NUTSLevel == 2));
                    }
                    // Apply the London fix
                    NUTS2RegionsInTheCountry.RemoveAll(x => x.RegionCode.StartsWith("UKI"));
                    NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode == "UKI").ToList());
                }
                if (country == "EU15")
                {
                    NUTS2RegionsInTheCountry.Clear();
                    foreach (string EUcountry in EU15Countries)
                    {
                        NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode.StartsWith(EUcountry) && x.NUTSLevel == 2));
                    }
                    // Apply the London fix
                    NUTS2RegionsInTheCountry.RemoveAll(x => x.RegionCode.StartsWith("UKI"));
                    NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode == "UKI").ToList());
                }
                if (country == "EU19")
                {
                    NUTS2RegionsInTheCountry.Clear();
                    foreach (string EUcountry in EU19Countries)
                    {
                        NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode.StartsWith(EUcountry) && x.NUTSLevel == 2));
                    }
                    // Apply the London fix
                    NUTS2RegionsInTheCountry.RemoveAll(x => x.RegionCode.StartsWith("UKI"));
                    NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode == "UKI").ToList());
                }
                if (country == "EU27")
                {
                    NUTS2RegionsInTheCountry.Clear();
                    foreach (string EUcountry in EU27Countries)
                    {
                        NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode.StartsWith(EUcountry) && x.NUTSLevel == 2));
                    }
                    // Apply the London fix
                    NUTS2RegionsInTheCountry.RemoveAll(x => x.RegionCode.StartsWith("UKI"));
                    NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode == "UKI").ToList());
                }

                if (country == "UK*")
                {
                    // There are three approaches, London, MegaLondon, and PartialMegaLondon
                    // Of these I think "London" is the best mix of fair and simple.
                    string approach = "LondonFix";

                    if (approach == "LondonFix")
                    {
                        // The UK has stupid NUTS regions, so we need to swap out the five NUTS2 regions for London with the NUTS1 region for London
                        NUTS2RegionsInTheCountry = CombinedData.Where(x => x.RegionCode.StartsWith("UK") && x.NUTSLevel == 2).ToList();
                        NUTS2RegionsInTheCountry.RemoveAll(x => x.RegionCode.StartsWith("UKI"));
                        NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode == "UKI").ToList());
                    }

                    /*
                    if (approach == "MegaLondon")
                    {
                        // We might also want to combine London, East, and South-East into a single mega region
                        // These are UKI, UKJ, UKH.
                        // First we remove all NUTS regions in those regions
                        NUTS2RegionsInTheCountry.RemoveAll(x => x.RegionCode.StartsWith("UKJ") || x.RegionCode.StartsWith("UKH") || x.RegionCode.StartsWith("UKI"));

                        // Then we create and add mega london
                        foreach (int year in UniqueYears)
                        {
                            CombinedData UKI = Combined.Where(x => x.RegionCode == "UKI" && x.Year == year).First();
                            CombinedData UKJ = Combined.Where(x => x.RegionCode == "UKJ" && x.Year == year).First();
                            CombinedData UKH = Combined.Where(x => x.RegionCode == "UKH" && x.Year == year).First();

                            CombinedData MegaLondon = new CombinedData();
                            MegaLondon.RegionCode = "UKZ";
                            MegaLondon.GDPTotal = UKI.GDPTotal + UKJ.GDPTotal + UKH.GDPTotal;
                            MegaLondon.IncomePerHead = Convert.ToInt32((UKI.IncomePerHead * UKI.Population + UKJ.IncomePerHead * UKJ.Population + UKH.IncomePerHead * UKH.Population) / (UKI.Population + UKJ.Population + UKH.Population));
                            MegaLondon.GDPPerHead = Convert.ToInt32((UKI.GDPPerHead * UKI.Population + UKJ.GDPPerHead * UKJ.Population + UKH.GDPPerHead * UKH.Population) / (UKI.Population + UKJ.Population + UKH.Population));
                            MegaLondon.Year = year;
                            MegaLondon.Population = UKI.Population + UKJ.Population + UKH.Population;

                            NUTS2RegionsInTheCountry.Add(MegaLondon);
                        }
                    }

                    // How about tweaking MegaLondon by removing Hampshire UKJ3 (and keeping UKJ1, UKJ2, UKJ4) and removing East Anglia UKH1 (and keeping UKH2, UKH3)?
                    if (approach == "PartialMegaLondon")
                    {
                        // First we remove all NUTS regions in the South-East regions
                        NUTS2RegionsInTheCountry.RemoveAll(x => x.RegionCode.StartsWith("UKJ") || x.RegionCode.StartsWith("UKH") || x.RegionCode.StartsWith("UKI"));
                        // Then add back in UKJ3 and UKH1

                        NUTS2RegionsInTheCountry.AddRange(Combined.Where(x => x.RegionCode == "UKJ3").ToList());
                        NUTS2RegionsInTheCountry.AddRange(Combined.Where(x => x.RegionCode == "UKH1").ToList());

                        //Now create partial mega London
                        List<string> PartialMegaLondonCodes = new List<string>() { "UKI", "UKJ1", "UKJ2", "UKJ4", "UKH2", "UKH3" };
                        foreach (int year in UniqueYears)
                        {
                            double GDPTotal = 0;
                            double IncomeTotal = 0;
                            double PopulationTotal = 0;

                            foreach (string regioncode in PartialMegaLondonCodes)
                            {
                                CombinedData region = Combined.Where(x => x.RegionCode == regioncode && x.Year == year).First();
                                GDPTotal += region.GDPTotal;
                                IncomeTotal += (region.IncomePerHead * region.Population);
                                PopulationTotal += region.Population;
                            }

                            CombinedData PartialMegaLondon = new CombinedData();
                            PartialMegaLondon.RegionCode = "UKY";
                            PartialMegaLondon.Year = year;
                            PartialMegaLondon.IncomePerHead = Convert.ToInt32(IncomeTotal / PopulationTotal);
                            PartialMegaLondon.Population = PopulationTotal;
                            PartialMegaLondon.GDPTotal = GDPTotal;
                            PartialMegaLondon.GDPPerHead = Convert.ToInt32(GDPTotal / PopulationTotal);
                            NUTS2RegionsInTheCountry.Add(PartialMegaLondon);
                        }
                    }
                    */
                }

                foreach (int year in UniqueYears)
                {
                    List<CombinedData> NUTS2RegionsInTheCountryThisYear = NUTS2RegionsInTheCountry.Where(x => x.Year == year).ToList();

                    DispersionOutput dispersionOutput = new DispersionOutput()
                    {
                        Year = year,
                        Country = loopcountry
                    };

                    dispersionOutput.GDPDispersion = calculateGDPDispersion(NUTS2RegionsInTheCountryThisYear);
                    dispersionOutput.GDPTheilT = calculateGDPTheilTIndex(NUTS2RegionsInTheCountryThisYear);
                    dispersionOutput.RegionsConsidered = NUTS2RegionsInTheCountryThisYear.Count;

                    if (CombinedData.Where(x => x.Year == year && x.RegionCode == country).FirstOrDefault() != null)
                    {
                        //dispersionOutput.IncomeDispersion = calculateIncomeDispersion(NUTS2RegionsInTheCountryThisYear, Combined.Where(x => x.Year == year && x.RegionCode == country).FirstOrDefault().IncomePerHead);
                        dispersionOutput.IncomeDispersion = calculateIncomeDispersion(NUTS2RegionsInTheCountryThisYear);
                        dispersionOutput.IncomeTheilT = calculateIncomeTheilTIndex(NUTS2RegionsInTheCountryThisYear);
                    }

                    DispersionOutputs.Add(dispersionOutput);
                }

            }
            return DispersionOutputs;
        }
        static double calculateGDPDispersion(List<CombinedData> ListOfRegions)
        {
            // we assume that the list of regions is complete, ie. fully covers the country with no overlaps
            
            double P = ListOfRegions.Sum(x => x.Population);
            double Y = ListOfRegions.Sum(x => x.GDPTotal) / ListOfRegions.Sum(x => x.Population);
            double D = 0;

            foreach (CombinedData region in ListOfRegions)
            {
                if (region.Population == 0)
                {
                    Console.WriteLine($"Region {region.CountryOrGrouping} has zero population during GDP dispersion calculation. This shouldn't happen!");
                }
                D += (Math.Abs(region.GDPPerHead - Y)) * (region.Population / P);
            }

            double Dispersion = 100 * D / Y;
            return Math.Round(Dispersion,1);
        }

        // Theil index https://www.tandfonline.com/doi/full/10.1080/17421772.2017.1343491
        // This method is unchecked and unsafe. It is included in the hope it will be checked and improved.
        static double calculateGDPTheilTIndex(List<CombinedData> ListOfRegions)
        {
            
            // we assume that the list of regions is complete, ie. fully covers the country with no overlaps
            double mu = ListOfRegions.Sum(x => x.GDPTotal) / ListOfRegions.Sum(x => x.Population);
            /*if (ListOfRegions.Count > 0)
            {
                mu = ListOfRegions.Average(x => x.GDPPerHead); // REALLY DON'T LIKE THIS
            }*/
            double N = ListOfRegions.Count;
            double T = 0;

            foreach (CombinedData region in ListOfRegions)
            {
                T += (region.Population/ ListOfRegions.Sum(x => x.Population)) * (region.GDPPerHead / mu) * (Math.Log((region.GDPPerHead) / mu));
            }

            double TheilT = T / N;
            return Math.Round(TheilT, 5);
        }

        // Theil index https://www.tandfonline.com/doi/full/10.1080/17421772.2017.1343491
        // This method is unchecked and unsafe. It is included in the hope it will be checked and improved.
        static double calculateIncomeTheilTIndex(List<CombinedData> ListOfRegions)
        {
            // we assume that the list of regions is complete, ie. fully covers the country with no overlaps
            double mu = ListOfRegions.Sum(x => x.IncomeTotal) / ListOfRegions.Sum(x => x.Population);
            if (ListOfRegions.Count > 0)
            {
                mu = ListOfRegions.Average(x => x.IncomePerHead); // REALLY DON'T LIKE THIS
            }
            double N = ListOfRegions.Count;
            double T = 0;

            foreach (CombinedData region in ListOfRegions)
            {
                T += (region.IncomePerHead / mu) * (Math.Log((region.IncomePerHead) / mu));
            }

            double TheilT = T / N;
            return Math.Round(TheilT, 5);
        }

        // I have concerns about Eurostat income data, and results from this method should be treated with caution.
        static double calculateIncomeDispersion(List<CombinedData> ListOfRegions)
        {
            // we assume that the list of regions is complete, ie. fully covers the country with no overlaps            
            double P = ListOfRegions.Sum(x => x.Population);
            double Y = ListOfRegions.Sum(x => x.IncomeTotal) / ListOfRegions.Sum(x => x.Population);
            double D = 0;

            foreach (CombinedData region in ListOfRegions)
            {
                D += (Math.Abs(region.IncomePerHead - Y)) * (region.Population / P);
            }

            double Dispersion = 100 * D / Y;
            return Math.Round(Dispersion, 1);
        }

    }

    public class HistoricGDPPerHeadPPS
    {
        public string NUTScode { get; set; }
        public int NUTSlevel { get; set; }
        public string regionName { get; set; }
        public double Value { get; set; }
        public int Year { get; set; }
    }

    public class Population
    {
        public int TIME { get; set; }
        public string GEO { get; set; }
        public double Value { get; set; }
    }

    public class DispersionOutput
    {
        public string Country { get; set; }
        public double GDPDispersion { get; set; }

        [Name("Income Dispersion (I am unsure about Eurostat income data).")]
        public double IncomeDispersion { get; set; }

        [Name("GDP Theil Index (unsafe, do not use without verification).")]
        public double GDPTheilT { get; set; }
        [Name("Income Theil Index (unsafe, do not use without verification).")]
        public double IncomeTheilT { get; set; }
        public int Year { get; set; }
        public int RegionsConsidered { get; set; }
    }

    public class CombinedData
    {
        public int Year { get; set; }
        public string RegionCode { get; set; }
        public string CountryOrGrouping { get; set; }
        public double GDPPerHead { get; set; }
        public double GDPTotal { get; set; }
        public int NUTSLevel { get; set; }
        public int IncomePerHead { get; set; }

        public double IncomeTotal { get; set; }
        public double Population { get; set; }
    }

    public class GDPTotalRaw
    {
        public int TIME { get; set; }
        public string GEO { get; set; }
        public string GEO_LABEL { get; set; }
        public string UNIT { get; set; }
        public string UNIT_LABEL { get; set; }
        public string Value { get; set; }
    }

    public class GDPPerHeadRaw
    {
        public int TIME { get; set; }
        public string GEO { get; set; }
        public string GEO_LABEL { get; set; }
        public string UNIT { get; set; }
        public string UNIT_LABEL { get; set; }
        public string Value { get; set; }
    }

    public class IncomeRaw
    {
        public int TIME { get; set; }
        public string GEO { get; set; }
        public string GEO_LABEL { get; set; }
        public string UNIT { get; set; }
        public string DIRECT { get; set; }
        public string NA_ITEM { get; set; }
        public string Value { get; set; }
    }
}
