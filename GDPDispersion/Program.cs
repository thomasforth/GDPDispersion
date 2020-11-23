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

            //List<GDPTotalRaw> RawGDPTotals;
            List<GDPPerHeadRaw> RawGDPPerHead;
            List<IncomeRaw> RawIncomes;

            Console.WriteLine("Loading GDP per person at PPS for small regions of Europe.");
            using (TextReader textReader = File.OpenText(@"Assets\nama_10r_2gdp_1_Data.csv"))
            {
                CsvReader csvReader = new CsvReader(textReader, CultureInfo.InvariantCulture);
                csvReader.Configuration.HeaderValidated = null;
                csvReader.Configuration.MissingFieldFound = null;
                RawGDPPerHead = new List<GDPPerHeadRaw>(csvReader.GetRecords<GDPPerHeadRaw>());
            }

            /*
            Console.WriteLine("Loading household income per person at PPS for small regions of Europe.");
            using (TextReader textReader = File.OpenText(@"Assets\nama_10r_2hhinc_1_Data.csv"))
            {
                CsvReader csvReader = new CsvReader(textReader, CultureInfo.InvariantCulture);
                RawIncomes = new List<IncomeRaw>(csvReader.GetRecords<IncomeRaw>());
            }
            */
            Console.WriteLine("Loading total household income at PPS for small regions of Europe.");
            using (TextReader textReader = File.OpenText(@"Assets\nama_10r_2hhinc_1_Data_Abs.csv"))
            {
                CsvReader csvReader = new CsvReader(textReader, CultureInfo.InvariantCulture);
                RawIncomes = new List<IncomeRaw>(csvReader.GetRecords<IncomeRaw>());
            }

            Console.WriteLine("Loading populations for small regions of Europe.");
            List<Population> Populations = new List<Population>();            
            using (TextReader textReader = File.OpenText("Assets/demo_r_d2jan_1_Data.csv"))
            {
                CsvReader csvReader = new CsvReader(textReader, CultureInfo.InvariantCulture);
                csvReader.Configuration.HeaderValidated = null;
                csvReader.Configuration.MissingFieldFound = null;
                Populations = csvReader.GetRecords<Population>().ToList();
            }

            Console.WriteLine("Loading historic GDP/head (at PPS) for small regions of Europe. This lets us create complete time series for countries such as France and The Netherlands which have changed their geographies since 2000.");
            List<HistoricGDPPerHeadPPS> HistoricGDPs = new List<HistoricGDPPerHeadPPS>();
            using (TextReader textReader = File.OpenText("Assets/euregionsabsolutepps_unpivotted.csv"))
            {
                CsvReader csvReader = new CsvReader(textReader, CultureInfo.InvariantCulture);
                HistoricGDPs = csvReader.GetRecords<HistoricGDPPerHeadPPS>().ToList();
            }

            // Remove 2015 and 2016 French data (we have new data that's better)
            HistoricGDPs.RemoveAll(x => x.Year >= 2015 && x.NUTScode.StartsWith("FR"));


            /*
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
            */

            List<CombinedData> CombinedCurrentData = new List<CombinedData>();

            List<int> DistinctYears = RawGDPPerHead.Select(x => x.TIME).ToList();
            DistinctYears.AddRange(RawIncomes.Select(x => x.TIME));
            DistinctYears = DistinctYears.Distinct().ToList();

            List<string> DistinctRegionCodes = RawGDPPerHead.Select(x => x.GEO).ToList();
            DistinctRegionCodes.AddRange(RawIncomes.Select(x => x.GEO));
            DistinctRegionCodes.AddRange(HistoricGDPs.Select(x => x.NUTScode));
            DistinctRegionCodes = DistinctRegionCodes.Distinct().ToList();
            foreach (int year in DistinctYears)
            {
                foreach (string regioncode in DistinctRegionCodes)
                {
                    HistoricGDPPerHeadPPS HistoricGDPPerHead = HistoricGDPs.Where(x => x.NUTScode == regioncode && x.Year == year).FirstOrDefault();
                    GDPPerHeadRaw GDPPerHead = RawGDPPerHead.Where(x => x.GEO == regioncode && x.TIME == year).FirstOrDefault();
                    IncomeRaw Income = RawIncomes.Where(x => x.GEO == regioncode && x.TIME == year).FirstOrDefault();
                    if (Income != null && Income.Value == ":") { Income = null; }
                    if (GDPPerHead != null && GDPPerHead.Value == ":") { GDPPerHead = null; }

                    if (HistoricGDPPerHead != null || GDPPerHead != null || Income != null)
                    {
                        CombinedData combinedData = new CombinedData()
                        {
                            Year = year,
                            RegionCode = regioncode
                        };
                        if (GDPPerHead != null)
                        {
                            combinedData.RegionName = GDPPerHead.GEO_LABEL;
                            if (GDPPerHead.Value.Contains(','))
                            {
                                combinedData.GDPPerHead = int.Parse(GDPPerHead.Value.Replace(",", ""));
                            }
                            if (GDPPerHead.GEO.StartsWith("EU") == false)
                            {
                                combinedData.NUTSLevel = GDPPerHead.GEO.Length - 2;
                            }
                        }
                        else if (HistoricGDPPerHead != null)
                        {
                            combinedData.RegionName = HistoricGDPPerHead.regionName;
                            combinedData.NUTSLevel = HistoricGDPPerHead.NUTSlevel;
                            combinedData.GDPPerHead = HistoricGDPPerHead.Value;
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
                            Console.WriteLine($"No population data found for {combinedData.RegionCode} in {combinedData.Year}.");
                        }
                        combinedData.GDPTotal = combinedData.Population * combinedData.GDPPerHead;

                        // Add Income (PPP) per head
                        if (Income != null)
                        {
                            combinedData.RegionName = Income.GEO_LABEL;
                            combinedData.IncomeTotal = 1000000 * double.Parse(Income.Value.Replace(",", ""));
                            combinedData.NUTSLevel = Income.GEO.Length - 2;
                        }
                        // Calculate Income total
                        if (combinedData.Population > 0)
                        {
                            combinedData.IncomePerHead = (int)Math.Round(combinedData.IncomeTotal / combinedData.Population, 0);
                        }
                        CombinedCurrentData.Add(combinedData);
                    }
                }
            }

            // Calculate dispersions for both historic and current datasets
            //List<DispersionOutput> HistoricDispersionOutputs = CalculateDispersions(HistoricCombinedData);
            List<DispersionOutput> DispersionOutputs = CalculateDispersions(CombinedCurrentData);


            // Merge the two outputs (prefering the dataset which was calculated using the highest number of regions)
            //List<DispersionOutput> CombinedDispersionOutputs = new List<DispersionOutput>();            
            //CombinedDispersionOutputs.AddRange(HistoricDispersionOutputs);
            /*
            foreach (DispersionOutput currentDispersionOutput in CurrentDispersionOutputs)
            {
                if (CombinedDispersionOutputs.Exists(x => x.Country == currentDispersionOutput.Country && x.Year == currentDispersionOutput.Year) == false)
                {
                    CombinedDispersionOutputs.Add(currentDispersionOutput);
                }
                else if (CombinedDispersionOutputs.Exists(x => x.Country == currentDispersionOutput.Country && x.Year == currentDispersionOutput.Year && x.RegionsConsidered < currentDispersionOutput.RegionsConsidered))
                {
                    CombinedDispersionOutputs.RemoveAll(x => x.Country == currentDispersionOutput.Country && x.Year == currentDispersionOutput.Year);
                    CombinedDispersionOutputs.Add(currentDispersionOutput);
                }
                else
                {
                    // the existing calculated dispersion was calculated from a higher number of regions than the potential replacement. The existing dispersion is retained.
                    CombinedDispersionOutputs.Add(currentDispersionOutput);
                }
            }
            */

            // Output the dispersion calculation results
            using (TextWriter writer = new StreamWriter($"CalculatedDispersions_LondonFixed.csv", false, System.Text.Encoding.UTF8))
            {
                var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                csv.WriteRecords(DispersionOutputs);
            }
        }

        static List<DispersionOutput> CalculateDispersions(List<CombinedData> CombinedData)
        {
            // All the data we need is now loaded in. Time to calculate Theil Index and GDP dispersion
            // https://en.wikipedia.org/wiki/Theil_index
            // 
            List<string> UniqueCountries = CombinedData.Select(x => x.RegionCode.Substring(0, 2)).Distinct().ToList();
            UniqueCountries.Remove("EU");
            UniqueCountries.Add("EA12");
            UniqueCountries.Add("EU15");
            UniqueCountries.Add("EU19");
            UniqueCountries.Add("EU27");
            UniqueCountries.Add("UK*");
            UniqueCountries.Add("Nordics");
            UniqueCountries.Add("North England");
            UniqueCountries.Add("Low Countries");

            List<int> UniqueYears = CombinedData.Select(x => x.Year).Distinct().OrderBy(x => x).ToList();
            List<DispersionOutput> DispersionOutputs = new List<DispersionOutput>();
            List<CombinedData> AllRawData = new List<CombinedData>();
            foreach (string country in UniqueCountries)
            {
                List<CombinedData> NUTS2RegionsInTheCountry = CombinedData.Where(x => x.RegionCode.StartsWith(country) && x.NUTSLevel == 2).ToList();
                // whole-EU calculations are a special case. The LondonFix is applied and all NUTS2 regions for nations within the EU are used.
                // CURRENTLY DOES NOT INCLUDE CROATIA (HR). THIS MAY NEED FIXING SOON!
                List<string> EA12Countries = new List<string>() { "AT", "BE", "DE", "EL", "ES", "FI", "FR", "IE", "IT", "LU", "NL", "PT"};
                List<string> EU15Countries = new List<string>() { "AT", "BE", "DE", "DK", "EL", "ES", "FI", "FR", "IE", "IT", "LU", "NL", "PT", "SE", "UK" };
                List<string> EU19Countries = new List<string>() { "AT", "BE", "DE", "DK", "EL", "ES", "FI", "FR", "IE", "IT", "LU", "NL", "PT", "SE", "UK", "SK", "PL", "HU", "CZ" };
                List<string> EU27Countries = new List<string>() { "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "EL", "ES", "FI", "FR", "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK", "UK" };
                List<string> Nordics = new List<string>() { "SE", "FI", "DK" };
                List<string> NorthEngland = new List<string>() { "UKE", "UKD", "UKC" };
                List<string> LowCountries = new List<string>() { "NL", "BE" };                

                // IRELAND is REMOVED BECAUSE ITS GDP DATA IS USELESS
                EA12Countries.Remove("IE");
                EU15Countries.Remove("IE");
                EU19Countries.Remove("IE");
                EU27Countries.Remove("IE");

                if (country == "Nordics")
                {
                    NUTS2RegionsInTheCountry.Clear();
                    foreach (string EUcountry in Nordics)
                    {
                        NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode.StartsWith(EUcountry) && x.NUTSLevel == 2));
                    }
                }

                if (country == "North England")
                {
                    NUTS2RegionsInTheCountry.Clear();
                    foreach (string EUcountry in NorthEngland)
                    {
                        NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode.StartsWith(EUcountry) && x.NUTSLevel == 2));
                    }
                }

                if (country == "Low Countries")
                {
                    NUTS2RegionsInTheCountry.Clear();
                    foreach (string EUcountry in LowCountries)
                    {
                        NUTS2RegionsInTheCountry.AddRange(CombinedData.Where(x => x.RegionCode.StartsWith(EUcountry) && x.NUTSLevel == 2));
                    }
                }

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
                }

                foreach (int year in UniqueYears)
                {
                    List<CombinedData> NUTS2RegionsInTheCountryThisYear = NUTS2RegionsInTheCountry.Where(x => x.Year == year).ToList();

                    DispersionOutput dispersionOutput = new DispersionOutput()
                    {
                        Year = year,
                        Country = country,
                        IncomeDispersion = double.NaN,
                        IncomeTheilT = double.NaN,
                        GDPDispersion = double.NaN,
                        GDPTheilT = double.NaN
                    };

                    foreach (CombinedData cd in NUTS2RegionsInTheCountryThisYear)
                    {
                        CombinedData newcd = new CombinedData()
                        {
                            CountryOrOtherGrouping = country,
                            Year = cd.Year,
                            GDPPerHead = cd.GDPPerHead,
                            GDPTotal = cd.GDPTotal,
                            IncomePerHead = cd.IncomePerHead,
                            IncomeTotal = cd.IncomeTotal,
                            Population = cd.Population,
                            RegionCode = cd.RegionCode,
                            RegionName = cd.RegionName,
                            NUTSLevel = cd.NUTSLevel,
                        };
                        AllRawData.Add(newcd);
                    }

                    if (NUTS2RegionsInTheCountryThisYear.Where(x => x.IncomePerHead > 0).Count() > 1)
                    {
                        dispersionOutput.IncomeDispersion = calculateIncomeDispersion(NUTS2RegionsInTheCountryThisYear);
                        dispersionOutput.IncomeTheilT = calculateIncomeTheilTIndex(NUTS2RegionsInTheCountryThisYear);
                        dispersionOutput.RegionsConsideredForGDP = NUTS2RegionsInTheCountryThisYear.Where(x => x.IncomePerHead > 0).Count();
                    }

                    if (NUTS2RegionsInTheCountryThisYear.Where(x => x.GDPPerHead > 0).Count() > 1)
                    {
                        dispersionOutput.GDPDispersion = calculateGDPDispersion(NUTS2RegionsInTheCountryThisYear);
                        dispersionOutput.GDPTheilT = calculateGDPTheilTIndex(NUTS2RegionsInTheCountryThisYear);
                        dispersionOutput.RegionsConsideredForGDP = NUTS2RegionsInTheCountryThisYear.Where(x => x.GDPPerHead > 0).Count();
                    }

                    DispersionOutputs.Add(dispersionOutput);
                }
            }

            // Output the prepared raw data for further analysis
            using (TextWriter writer = new StreamWriter($"PreparedDataForDispersions.csv", false, System.Text.Encoding.UTF8))
            {
                var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                csv.WriteRecords(AllRawData);
            }

            return DispersionOutputs;
        }
        static double calculateGDPDispersion(List<CombinedData> ListOfRegions)
        {
            // We remove regions with zero population or zero GDP
            List<CombinedData> CleanListOfRegions = ListOfRegions.Where(x => x.Population != 0 && x.GDPTotal != 0).ToList();

            // we assume that the list of regions is complete, ie. fully covers the country with no overlaps            
            double P = CleanListOfRegions.Sum(x => x.Population);
            double Y = CleanListOfRegions.Sum(x => x.GDPTotal) / CleanListOfRegions.Sum(x => x.Population);
            double D = 0;

            foreach (CombinedData region in CleanListOfRegions)
            {
                if (region.Population == 0)
                {
                    Console.WriteLine($"Region {region.RegionName} has zero population during GDP dispersion calculation. This shouldn't happen!");
                }
                if (region.GDPPerHead == 0)
                {
                    Console.WriteLine($"Region {region.RegionName} has zero GDP per head during GDP dispersion calculation. This shouldn't happen!");
                }
                if (region.GDPTotal == 0)
                {
                    Console.WriteLine($"Region {region.RegionName} has zero total GDP during GDP dispersion calculation. This shouldn't happen!");
                }
                D += (Math.Abs(region.GDPPerHead - Y)) * (region.Population / P);
            }

            double Dispersion = 100 * D / Y;
            Dispersion = Math.Round(Dispersion, 1);
            return Dispersion;
        }

        // Theil index https://www.tandfonline.com/doi/full/10.1080/17421772.2017.1343491
        // This method is unchecked and unsafe. It is included in the hope it will be checked and improved.
        static double calculateGDPTheilTIndex(List<CombinedData> ListOfRegions)
        {
            // We remove regions with zero population or zero GDP
            List<CombinedData> CleanListOfRegions = ListOfRegions.Where(x => x.Population != 0 && x.GDPTotal != 0).ToList();

            // we assume that the list of regions is complete, ie. fully covers the country with no overlaps
            double mu = CleanListOfRegions.Sum(x => x.GDPTotal) / CleanListOfRegions.Sum(x => x.Population);
            /*if (ListOfRegions.Count > 0)
            {
                mu = ListOfRegions.Average(x => x.GDPPerHead); // REALLY DON'T LIKE THIS
            }*/
            double N = CleanListOfRegions.Count;
            double T = 0;

            foreach (CombinedData region in CleanListOfRegions)
            {
                T += (region.Population/ CleanListOfRegions.Sum(x => x.Population)) * (region.GDPPerHead / mu) * (Math.Log((region.GDPPerHead) / mu));
            }

            double TheilT = T / N;
            return Math.Round(TheilT, 5);
        }

        // Theil index https://www.tandfonline.com/doi/full/10.1080/17421772.2017.1343491
        // This method is unchecked and unsafe. It is included in the hope it will be checked and improved.
        static double calculateIncomeTheilTIndex(List<CombinedData> ListOfRegions)
        {
            // We remove regions with zero population or zero income
            List<CombinedData> CleanListOfRegions = ListOfRegions.Where(x => x.Population != 0 && x.IncomeTotal != 0).ToList();

            // we assume that the list of regions is complete, ie. fully covers the country with no overlaps
            double mu = CleanListOfRegions.Sum(x => x.IncomeTotal) / CleanListOfRegions.Sum(x => x.Population);
            if (CleanListOfRegions.Count > 0)
            {
                mu = CleanListOfRegions.Average(x => x.IncomePerHead); // REALLY DON'T LIKE THIS
            }
            double N = CleanListOfRegions.Count;
            double T = 0;

            foreach (CombinedData region in CleanListOfRegions)
            {
                T += (region.IncomePerHead / mu) * (Math.Log((region.IncomePerHead) / mu));
            }

            double TheilT = T / N;
            return Math.Round(TheilT, 5);
        }

        // I have concerns about Eurostat income data, and results from this method should be treated with caution.
        static double calculateIncomeDispersion(List<CombinedData> ListOfRegions)
        {
            // We remove regions with zero population or zero income
            List<CombinedData> CleanListOfRegions = ListOfRegions.Where(x => x.Population != 0 && x.IncomeTotal != 0).ToList();

            // we assume that the list of regions is complete, ie. fully covers the country with no overlaps            
            double P = CleanListOfRegions.Sum(x => x.Population);
            double Y = CleanListOfRegions.Sum(x => x.IncomeTotal) / CleanListOfRegions.Sum(x => x.Population);
            double D = 0;

            foreach (CombinedData region in CleanListOfRegions)
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
        public int RegionsConsideredForIncome { get; set; }
        public int RegionsConsideredForGDP { get; set; }
    }

    public class CombinedData
    {
        public int Year { get; set; }
        public string RegionCode { get; set; }
        public string RegionName { get; set; }
        public double GDPPerHead { get; set; }
        public double GDPTotal { get; set; }
        public int NUTSLevel { get; set; }
        public int IncomePerHead { get; set; }

        public double IncomeTotal { get; set; }
        public double Population { get; set; }
        public string CountryOrOtherGrouping { get; set; }
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
