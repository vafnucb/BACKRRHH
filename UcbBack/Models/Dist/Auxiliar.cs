using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{

    public class Auxiliar
    {
       
        public int Id { set; get; }

        public string Name { get; set; }

        public string Abr { get; set; }

    }
}