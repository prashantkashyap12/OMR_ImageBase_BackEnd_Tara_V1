using System.ComponentModel.DataAnnotations;

namespace SQCScanner.Modal
{
    public class ImgTemp
    {
        [Key]
        public int Id {get; set;}
        public string CreateAt { get; set; }
        public string FileName { get; set; }
        public string imgPath { get; set; }
        public string JsonPath { get; set; }
        public string discription { get; set; }


    }
}
