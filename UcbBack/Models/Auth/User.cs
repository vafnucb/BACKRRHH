using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Configuration;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Auth
{
    [CustomSchema("User")]
    public class CustomUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        [Required]
        public string UserPrincipalName { get; set; }
        public People People { get; set; }
        public int PeopleId { get; set; }
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ?TokenCreatedAt { get; set; }
        public DateTime ?RefreshTokenCreatedAt { get; set; }
        public string AutoGenPass { get; set; }
        public string TipoLicenciaSAP { get; set; }
        public bool? CajaChica { get; set; }
        public bool? SolicitanteCompras { get; set; }
        public bool? AutorizadorCompras { get; set; }
        public bool? Rendiciones { get; set; }
        public bool? RendicionesDolares { get; set; }
        [ForeignKey("AuthPeopleId")]
        public People Auth { get; set; }
        public int? AuthPeopleId { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_User_sqs\".nextval FROM DUMMY;").ToList()[0];
        }

        public void GetPerfilCajaChica(out int id, out string nom)
        {
            var reg = this.People.GetLastContract().Dependency.BranchesId;
            switch (reg)
            {
                case 2: //TJA
                    id = 11;
                    nom = "CAJA_TJA-Moneda Local(BS)";
                    break;
                case 3: //CBB
                    id = 9;
                    nom = "CAJA_CBB-Moneda Local(BS)";
                    break;
                case 6: //UCE
                    id = 13;
                    nom = "CAJA_UCE-Moneda Local(BS)";
                    break;
                case 16: //SC
                    id = 10;
                    nom = "CAJA_SCZ-Moneda Local(BS)";
                    break;
                case 17: //LP
                    id = 8;
                    nom = "CAJA_LPZ-Moneda Local(BS)";
                    break;
                case 18: //EPC
                    id = 12;
                    nom = "CAJA_EPC-Moneda Local(BS)";
                    break;
                case 22: //TEO
                    id = 21;
                    nom = "CAJA_TEO-Moneda Local(BS)";
                    break;
                default:
                    id = 0;
                    nom = "";
                    break;
            }
        }

        public void GetPerfilRendiciones(out int id, out string nom)
        {
            var reg = this.People.GetLastContract().Dependency.BranchesId;
            switch (reg)
            {
                case 2: //TJA
                    id = 4;
                    nom = "RENDICIONES_TJA-Moneda Local(BS)";
                    break;
                case 3: //CBB
                    id = 2;
                    nom = "RENDICIONES_CBB-Moneda Local(BS)";
                    break;
                case 6: //UCE
                    id = 7;
                    nom = "RENDICIONES_UCE-Moneda Local(BS)";
                    break;
                case 16: //SC
                    id = 3;
                    nom = "RENDICIONES_SCZ-Moneda Local(BS)";
                    break;
                case 17: //LP
                    id = 1;
                    nom = "RENDICIONES_LPZ-Moneda Local(BS)";
                    break;
                case 18: //EPC
                    id = 5;
                    nom = "RENDICIONES_EPC-Moneda Local(BS)";
                    break;
                case 22: //TEO
                    id = 6;
                    nom = "RENDICIONES_TEO-Moneda Local(BS)";
                    break;
                default:
                    id = 0;
                    nom = "";
                    break;
            }
        }

        public void GetPerfilRendicionesDolares(out int id, out string nom)
        {
            var reg = this.People.GetLastContract().Dependency.BranchesId;
            switch (reg)
            {
                case 2: //TJA
                    id = 16;
                    nom = "RENDICIONES_TJA-Moneda Sistema(USD)";
                    break;
                case 3: //CBB
                    id = 18;
                    nom = "RENDICIONES_CBB-Moneda Sistema(USD)";
                    break;
                case 6: //UCE
                    id = 15;
                    nom = "RENDICIONES_UCE-Moneda Sistema(USD)";
                    break;
                case 16: //SC
                    id = 19;
                    nom = "RENDICIONES_SCZ-Moneda Sistema(USD)";
                    break;
                case 17: //LP
                    id = 17;
                    nom = "RENDICIONES_LPZ-Moneda Sistema(USD)";
                    break;
                case 18: //EPC
                    id = 20;
                    nom = "RENDICIONES_EPC-Moneda Sistema(USD)";
                    break;
                case 22: //TEO
                    id = 14;
                    nom = "RENDICIONES_TEO-Moneda Sistema(USD)";
                    break;
                default:
                    id = 0;
                    nom = "";
                    break;
            }
        }

        public string GetContador()
        {
            var reg = this.People.GetLastContract().Dependency.BranchesId;
            switch (reg)
            {
                case 2: //TJA
                    return "DELGADILLO APARICIO EDGAR";
                case 3: //CBB
                    return "PEREDO GUMUCIO JONNY HAAMET";
                case 6: //UCE
                    return "AGUIRRE RIOS GLORIA DORIS";
                case 16: //SC
                    return "CAMACHO MORENO MARGARITA MARCIA";
                case 17: //LP
                    return "ALDUNATE MORALES NANCY JAEL";
                case 18: //EPC
                    return "ALIAGA CALCINA LIZ MARGOTH";
                case 22: //TEO
                    return "PEREDO GUMUCIO JONNY HAAMET";
                default:
                    return null;
            }
        }
        
        public void CreateInRendiciones(ApplicationDbContext _context)
        {

            _context = _context == null ? new ApplicationDbContext() : _context;
            //var HashPass = user.AutoGenPass;
            var HashPass = _context.Database.SqlQuery<string>("select  to_varchar(to_binary('" + this.AutoGenPass +"')) from dummy").ToList()[0].ToLower();
            var fullName = this.People.GetFullName();
            var exist = _context.Database.SqlQuery<string>("SELECT \"U_IdU\" from \"" + ConfigurationManager.AppSettings["RendicionesSchema"] + "\".\"REND_U\"" +
                                                           " where \"U_Login\" = '" + this.UserPrincipalName + "';").ToList().Any();
            if (!exist)
            {
                int nextId = _context.Database.SqlQuery<int>("SELECT \"" + ConfigurationManager.AppSettings["RendicionesSchema"] + "\".\"DIMRENDUSUARIO_SEQ\".nextval FROM DUMMY;").ToList()[0];
                string query = "insert into  " +
                               " 	" + ConfigurationManager.AppSettings["RendicionesSchema"] + ".rend_u ( " +
                               " 			\"U_IdU\", " +
                               " 			\"U_Login\", " +
                               " 			\"U_Pass\", " +
                               " 			\"U_SuperUser\", " +
                               " 			\"U_AppRend\", " +
                               " 			\"U_AppExtB\", " +
                               " 			\"U_AppUpLA\", " +
                               " 			\"U_GenDocPre\", " +
                               " 			\"U_NomUser\", " +
                               " 			\"U_NomSup\", " +
                               " 			\"U_Estado\", " +
                               " 			\"U_AppConf\", " +
                               " 			\"U_CardCode\", " +
                               " 			\"U_CardName\" " +
                               " 		) " +
                               " 	values ( " +
                               " 			" + nextId + ", " +
                               " 			'" + this.UserPrincipalName + "', " +
                               " 			'" + HashPass + "', " +
                               " 			0, " +
                               " 			1, " +
                               " 			0, " +
                               " 			0, " +
                               " 			0, " +
                               " 			'" + fullName + "', " +
                               " 			'" + this.GetContador() + "', " +
                               " 			1, " +
                               " 			0, " +
                               " 			'R" + this.People.CUNI + "', " +
                               " 			'R" + this.People.CUNI + "-" + fullName + "' " +
                               " 		) ";

                var res = _context.Database.ExecuteSqlCommand(query);
            }
        }

        public void updatePerfilesRend(ApplicationDbContext _context)
        {
            this.UpdateRendicionesAuth(_context);
            this.UpdatePerfilCajaChica(_context);
            this.UpdatePerfilRendiciones(_context);
            this.UpdatePerfilRendicionesDolares(_context);
        }

        public void UpdateRendicionesAuth(ApplicationDbContext _context)
        {
            string query = "UPDATE " +
                           " " + ConfigurationManager.AppSettings["RendicionesSchema"] + ".rend_u " +
                           " SET " +
                           " \"U_AppRend\" = " + (this.Rendiciones.Value? "1":"0") +
                           " Where \"U_Login\" = '" + this.UserPrincipalName + "'";
            var res = _context.Database.ExecuteSqlCommand(query);
        }


        public string GetUserIdInRend(ApplicationDbContext _context)
        {
            var nextId = _context.Database.SqlQuery<string>("SELECT \"U_IdU\" from \"" + ConfigurationManager.AppSettings["RendicionesSchema"] + "\".\"REND_U\"" +
                                                            " where \"U_Login\" = '" + this.UserPrincipalName + "';").ToList()[0];
            return nextId;
        }

        public void AddPerfilInRend(ApplicationDbContext _context, int idperfil, string nomperfil)
        {
            var nextId = this.GetUserIdInRend(_context);
            var exist = _context.Database.SqlQuery<string>("SELECT \"U_IDUSUARIO\" from \"" + ConfigurationManager.AppSettings["RendicionesSchema"] + "\".\"REND_PRM\"" +
                                                           " where \"U_IDUSUARIO\" = " + nextId + "" +
                                                           " and  \"U_IDPERFIL\" = " + idperfil + ";").ToList().Any();
            if (!exist)
            {
                string query = "insert into \"" + ConfigurationManager.AppSettings["RendicionesSchema"] + "\".\"REND_PRM\" (U_IDUSUARIO, U_IDPERFIL, U_NOMBREPERFIL) " +
                               " values (" +
                               nextId + ", " +
                               idperfil + ",'" +
                               nomperfil + "'" +
                               ")";
                var res = _context.Database.ExecuteSqlCommand(query);
            }
        }

        public void RemovePerfilInRend(ApplicationDbContext _context, int idperfil, string nomperfil)
        {
            var nextId = this.GetUserIdInRend(_context);

            string query = "DELETE FROM \"" + ConfigurationManager.AppSettings["RendicionesSchema"] + "\".\"REND_PRM\" " +
                            " WHERE " +
                            " U_IDUSUARIO = " + nextId +
                            " AND U_IDPERFIL = " + idperfil;
            var res = _context.Database.ExecuteSqlCommand(query);
        }

        public void UpdatePerfilCajaChica(ApplicationDbContext _context)
        {
            int idperfil;
            string nomperfil;
            this.GetPerfilCajaChica(out idperfil, out nomperfil);
            if (this.CajaChica.Value)
            {
                this.AddPerfilInRend(_context,idperfil,nomperfil);
            }
            else
            {
                this.RemovePerfilInRend(_context,idperfil,nomperfil);
            }
        }

        public void UpdatePerfilRendiciones(ApplicationDbContext _context)
        {
            int idperfil;
            string nomperfil;
            this.GetPerfilRendiciones(out idperfil, out nomperfil);
            if (this.Rendiciones.Value)
            {
                this.AddPerfilInRend(_context, idperfil, nomperfil);
            }
            else
            {
                this.RemovePerfilInRend(_context, idperfil, nomperfil);
            }
        }

        public void UpdatePerfilRendicionesDolares(ApplicationDbContext _context)
        {
            int idperfil;
            string nomperfil;
            this.GetPerfilRendicionesDolares(out idperfil, out nomperfil);
            if (this.RendicionesDolares.Value)
            {
                this.AddPerfilInRend(_context, idperfil, nomperfil);
            }
            else
            {
                this.RemovePerfilInRend(_context, idperfil, nomperfil);
            }
        }


    }
}