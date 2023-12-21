using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations.Schema;
using System.Configuration;

namespace UcbBack.Models.Not_Mapped.CustomDataAnnotations
{
    public class CustomSchema : System.ComponentModel.DataAnnotations.Schema.TableAttribute
    {
        public new static String Schema = ConfigurationManager.AppSettings["SystemSchema"];
        public static String PreFix = "";

        public CustomSchema(string name) : base(addSchema(name))
        {
            Schema = ConfigurationManager.AppSettings["SystemSchema"];
        }

        private static string addSchema(string table)
        {
            return Schema + "." + PreFix + table;
        }
    }
}