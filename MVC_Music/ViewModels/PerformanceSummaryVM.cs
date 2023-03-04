using Microsoft.EntityFrameworkCore.Query;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;



namespace MVC_Music.ViewModels
{
    public class PerformanceSummaryVM
    {
        public int ID { get; set; }

        // a. Formal Name
        [DisplayName("Musician")]
        public string FormalName
        {
            get
            {
                return LastName + ", " + FirstName
                    + (string.IsNullOrEmpty(MiddleName) ? "" :
                        (" " + (char?)MiddleName[0] + ".").ToUpper());
            }
        }

        public string FirstName { get; set; }

        public string MiddleName { get; set; }

        public string LastName { get; set; }

        // b. Average Fee Paid
        [DisplayName("Average Earnings")]
        [DataType(DataType.Currency)]
        public double AvgFeePaid { get; set; }

        // c. Highest Fee Paid
        [DisplayName("Highest Fee Paid")]
        [DataType(DataType.Currency)]
        public double HighestFeePaid { get; set; }

        // d. Lowest Fee Paid
        [DisplayName("Lowest Fee Paid")]
        [DataType(DataType.Currency)]
        public double LowestFeePaid { get; set; }

        // e. Total number of performances
        [DisplayName("Number of Performances")]
        public int NumberOfPerformances { get; set; }

        // f. Total number of songs they performed on
        [DisplayName("Number of Songs")]
        public int NumberOfSongs { get; set; }
    }

}
