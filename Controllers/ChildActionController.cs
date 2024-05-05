﻿using FalaKAPP.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.Text;

//using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static QRCodes.Controllers.QrCodeController;

namespace FalaKAPP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChildActionController : ControllerBase
    {
        //link child to thier parent 
        [HttpPut("LinkChildByApplication")]
        public IActionResult linkchild([FromForm] int parentuserid, [FromForm] int childid, [FromForm] string kinshipT, [FromForm] int Boundry, [FromForm] string AdditionalInformation)
        {
            int affectedRows = 0;
            bool insertfollowchild = false;
            bool isMainPersonInChargeIDExists = DatabaseSettings.isMainPersonInChargeIDExists(childid);

            if (!isMainPersonInChargeIDExists)
            {
                if (DatabaseSettings.isIdExists(parentuserid) && DatabaseSettings.isIdExists(childid))
                {
                    using (SqlConnection conn = new SqlConnection(DatabaseSettings.dbConn))
                    {
                    
                        string sql = "UPDATE PersonChilds SET MainPersonInChargeID = @UserID, kinshipT = @KinshipT, Boundry = @Boundry, AdditionalInformation = @AdditionalInformation WHERE ChildID = @ChildID";
                        using (SqlCommand command = new SqlCommand(sql, conn))
                        {
                            command.Parameters.AddWithValue("@UserID", parentuserid);
                            command.Parameters.AddWithValue("@ChildID", childid);
                            command.Parameters.AddWithValue("@KinshipT", kinshipT);
                            command.Parameters.AddWithValue("@Boundry", Boundry);
                            command.Parameters.AddWithValue("@AdditionalInformation", AdditionalInformation);

                            conn.Open();
                            affectedRows = command.ExecuteNonQuery();
                        }

                        insertfollowchild = SettingController.insertorupdateAppMethod(childid, parentuserid);
                    }
                }
                if (affectedRows > 0 && insertfollowchild)
                {
                    return Ok("link success");
                }
                else
                {

                    return BadRequest("not linked");

                }



            }

            else if (isMainPersonInChargeIDExists)
            {
                return BadRequest("You cannot link the child. Try requesting tracking permission ");
            }
            else
            {
                return BadRequest("not linked");
            }

        }

      //link by verification code will be invoked when user enter 4  digit code for link by application
        [HttpGet("verify_verification_code")]
        public IActionResult verify_verification_code(int ChildID, int VerificationCode)
        {
            using (SqlConnection conn = new SqlConnection(DatabaseSettings.dbConn))
            {
                conn.Open();
                string sql = "SELECT * FROM PersonChilds WHERE ChildID = @ChildID ";
                SqlCommand Comm = new SqlCommand(sql, conn);
                Comm.Parameters.AddWithValue("@ChildID", ChildID);
                
                SqlDataReader reader = Comm.ExecuteReader();
                int verify = reader.GetInt32(reader.GetOrdinal("VerificationCode"));
                if (reader.Read() && verify == VerificationCode)
                {
                    reader.Close();
                    return Ok();
                }
                else
                {
                    reader.Close();
                    return BadRequest();
                }
            }
        }

        //to get children information and display result in home list 
        [HttpGet("ChildHome/{UserID}")]
        public ActionResult<string> Getchild(int UserID)
        {
            SqlConnection conn = new SqlConnection(DatabaseSettings.dbConn);
            conn.Open();

            //query 
            string sql = "SELECT ch.ChildId, ch.mainImagePath, kinshipT, pu.FullName AS childName, pu.Gender, ch.YearOfBirth, pr.PhoneNumber AS parentnumber, ch.isConnect, ch.Boundry, ch.Longitude, ch.Latitude FROM PersonChilds ch, PersonUsers pu, PersonUsers pr WHERE MainPersonInChargeID = @UserID AND (ch.ChildID = pu.UserID) AND MainPersonInChargeID = pr.UserID";

            SqlCommand Comm = new SqlCommand(sql, conn);
            Comm.Parameters.AddWithValue("@UserID", UserID);

            SqlDataReader reader = Comm.ExecuteReader();

            StringBuilder resultBuilder = new StringBuilder();
            resultBuilder.AppendLine("{"); // Start of JSON object

            while (reader.Read())
            {
                float? latitude = null;
                if (!reader.IsDBNull(reader.GetOrdinal("Latitude")))
                {
                    latitude = (float)reader.GetDouble(reader.GetOrdinal("Latitude"));
                }

                float? longitude = null;
                if (!reader.IsDBNull(reader.GetOrdinal("Longitude")))
                {
                    longitude = (float)reader.GetDouble(reader.GetOrdinal("Longitude"));
                }

                resultBuilder.AppendLine($"\"child{reader.GetInt32(reader.GetOrdinal("ChildId"))}\": {{"); // Start of child object with unique key

                resultBuilder.AppendLine($"\"mainImagePath\": \"{reader.GetString(reader.GetOrdinal("mainImagePath"))}\",");
                resultBuilder.AppendLine($"\"kinshipT\": \"{reader.GetString(reader.GetOrdinal("kinshipT"))}\",");
                resultBuilder.AppendLine($"\"childName\": \"{reader.GetString(reader.GetOrdinal("childName"))}\",");
                resultBuilder.AppendLine($"\"gender\": \"{reader.GetString(reader.GetOrdinal("Gender"))}\",");
                resultBuilder.AppendLine($"\"yearOfBirth\": {reader.GetInt32(reader.GetOrdinal("YearOfBirth"))},");
                resultBuilder.AppendLine($"\"boundry\": {reader.GetInt32(reader.GetOrdinal("Boundry"))},");
                resultBuilder.AppendLine($"\"parentnumber\": \"{reader.GetInt32(reader.GetOrdinal("parentnumber"))}\",");
                resultBuilder.AppendLine($"\"isConnect\": \"{reader.GetString(reader.GetOrdinal("isConnect"))}\",");
                resultBuilder.AppendLine($"\"latitude\": {(latitude.HasValue ? latitude.ToString() : "null")},");
                resultBuilder.AppendLine($"\"longitude\": {(longitude.HasValue ? longitude.ToString() : "null")}");

                resultBuilder.AppendLine("}},"); // End of child object
            }

            conn.Close();

            if (resultBuilder[resultBuilder.Length - 3] == ',')
            {
                resultBuilder.Remove(resultBuilder.Length - 3, 1); // Remove the trailing comma
            }

            resultBuilder.AppendLine("}"); // End of JSON object

            if (resultBuilder.Length > 2)
            {
                return Ok(resultBuilder.ToString());
            }

            return NotFound("Link your children");
        }

        //to get children information and display result in home list 
        [HttpGet("testChildHome/{UserID}")]
        public ActionResult<IEnumerable<object>> testGetchild(int UserID)
        {
            SqlConnection conn = new SqlConnection(DatabaseSettings.dbConn);
            conn.Open();

            //query 
            string sql = "select ch.ChildId ,ch.mainImagePath, kinshipT ,pu.FullName as childName,pu.Gender, ch.YearOfBirth ,pr.PhoneNumber as parentnumber ,ch.isConnect , ch.Boundry , ch.Longitude, ch.Latitude from PersonChilds ch ,PersonUsers pu , PersonUsers pr where MainPersonInChargeID = @UserID AND (ch.ChildID = pu.UserID) AND MainPersonInChargeID = pr.UserID";
            

            SqlCommand Comm = new SqlCommand(sql, conn);
            Comm.Parameters.AddWithValue("@UserID", UserID);

            SqlDataReader reader = Comm.ExecuteReader();

            List<object> childsprofile = new List<object>();
            while (reader.Read())
            {
                float? Latitude = null;
                if (!reader.IsDBNull(reader.GetOrdinal("Latitude")))
                {
                    Latitude = (float)reader.GetDouble(reader.GetOrdinal("Latitude"));
                }
                // Nullable float type
                float? longitude = null;

                if (!reader.IsDBNull(reader.GetOrdinal("Longitude")))
                {
                    longitude = (float)reader.GetDouble(reader.GetOrdinal("Longitude"));
                }
                var child = new
                {
                    childid = reader.GetInt32(reader.GetOrdinal("ChildID")),
                    MainImagePath = reader.GetString(reader.GetOrdinal("MainImagePath")),
                    kinshipT = reader.GetString(reader.GetOrdinal("kinshipT")),
                    childName = reader.GetString(reader.GetOrdinal("childName")),
                    Gender = reader.GetString(reader.GetOrdinal("Gender")),
                    YearOfBirth = reader.GetInt32(reader.GetOrdinal("YearOfBirth")),
                    Boundry = reader.GetInt32(reader.GetOrdinal("Boundry")),
                    parentnumber = reader.GetInt32(reader.GetOrdinal("parentnumber")),
                    isConnect = reader.GetString(reader.GetOrdinal("isConnect")),
                    Latitude = Latitude,
                    Longitude = longitude,
            };
                

                childsprofile.Add(child);
            }

            conn.Close();

            if (childsprofile.Count > 0)
            {
                return Ok(childsprofile);
            }

            return NotFound("Link your children");
        }


        [HttpPut("updateChildLocation")]
        public IActionResult updateChildLocation(int childID, float Longitude , float Latitude)
        {
            using (SqlConnection conn = new SqlConnection(DatabaseSettings.dbConn))
            {
                conn.Open();
                string sql = "UPDATE PersonChilds SET Longitude = @Longitude , Latitude = @Latitude WHERE ChildID = @ChildID";
                SqlCommand command = new SqlCommand(sql, conn);
                command.Parameters.AddWithValue("@ChildID", childID);
                command.Parameters.AddWithValue("@Longitude", Longitude);
                command.Parameters.AddWithValue("@Latitude", Latitude);

                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    return Ok();
                }
                else
                {
                    return BadRequest();
                }
            }
        }



    }

    }


