using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("AsignacionCarga")] // <-- table name in HANA
    public class AsignacionCarga
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public int AsigProcesoId { get; set; }
        public AsigProceso AsigProceso { get; set; }

        public string CiDocente { get; set; }


        public string PrimerApellido { get; set; }


        public string SegundoApellido { get; set; }

        public string TercerApellido { get; set; }


        public string Nombres { get; set; }


        /// Periodo (debería corresponder a PERIODOSAP en ADMNAL.T_REG_PARALELOS_NS).

        public string Periodo { get; set; }

        /// Sigla (SIGLA en ADMNAL.T_REG_PARALELOS_NS).
        public string Sigla { get; set; }

        /// Codigo_Paralelo (CODIGOSAP en ADMNAL.T_REG_PARALELOS_NS).

        public string CodigoParalelo { get; set; }

        /// Paralelo (NUMPARALELO en ADMNAL.T_REG_PARALELOS_NS).
        public string Paralelo { get; set; }

        public decimal HorasSemana { get; set; }

        public decimal HorasMes { get; set; }


        /// Unidad_Organizacional (CODUNIDADORGANIZACIONAL en ADMNAL.T_REG_PARALELOS_NS).

        public string UnidadOrganizacional { get; set; }

        /// Sede (SEDE en ADMNAL.T_REG_PARALELOS_NS).

        public string Sede { get; set; }

        public decimal CostoHora { get; set; }
       

        public int CantidadMeses { get; set; }
        /// Número de contrato que se asignará después de que
        /// la carga y las validaciones del archivo sean exitosas.
        /// Puede ser común al proceso o por fila, según tu lógica.

        public string NumeroContrato { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("Observaciones")]
        public string Observaciones { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            // Ajusta el nombre de la secuencia según lo que creen en HANA
            return _context.Database
                .SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Asig_AsignacionCarga_sqs\".nextval FROM DUMMY;")
                .ToList()[0];
        }
    }
}
