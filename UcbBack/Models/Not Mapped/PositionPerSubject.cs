using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    public class PositionSubjectViewModel
    {
        public int People_Id { get; set; }
        public string NameAbr { get; set; }
        public string Result { get; set; }
    }
}