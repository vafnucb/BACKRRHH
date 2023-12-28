using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using System.Data;
using System.Reflection;


namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    [NotMapped]
    public class AsesoriaDocenteViewModel
    {
        public int Id { get; set; }
        public string TeacherFullName { get; set; }
        public string TeacherCUNI { get; set; }
        public string TeacherBP { get; set; }
        public string Categoría { get; set; }
        public string Acta { get; set; }
        public DateTime? ActaFecha { get; set; }
        public int? BranchesId { get; set; }
        public string Regional { get; set; }
        public string Carrera { get; set; }
        public string DependencyCod { get; set; }
        public int? Horas { get; set; }
        public decimal MontoHora { get; set; }
        public decimal TotalNeto { get; set; }
        public decimal TotalBruto { get; set; }
        public string StudentFullName { get; set; }
        public int Mes { get; set; }
        public int Gestion { get; set; }
        public string Origen { get; set; }
        public string Observaciones { get; set; }
        public decimal? Deduccion { get; set; }
        public decimal? IUE { get; set; }
        public decimal? IT { get; set; }
        public decimal? IUEExterior { get; set; }
        public string Modalidad { get; set; }
        public string MesLiteral { get; set; }
        public string TipoTarea { get; set; }
        public string TipoPago { get; set; }
        public string Cod { get; set; }
        public string Ignore { get; set; }

        // Agregando nuevo campo para extranjero
        // public bool Extranjero { get; set; }





        public DataTable CreateDataTable<T>(IEnumerable<T> list)
        {
            Type type = typeof(T);
            //así se obtiene los nombres de las propiedades de una entidad
            var properties = type.GetProperties();

            DataTable dataTable = new DataTable();
            foreach (PropertyInfo info in properties)
            {
                //Se agregan los títulos de las columnas
                dataTable.Columns.Add(new DataColumn(info.Name, Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType));
            }

            foreach (T entity in list)
            {
                object[] values = new object[properties.Length];
                for (int i = 0; i < properties.Length; i++)
                {
                    //Se agrega fila por fila la lista con las entidades de la asesoría
                    values[i] = properties[i].GetValue(entity);
                }

                dataTable.Rows.Add(values);
            }

            return dataTable;
        }
    }

}
