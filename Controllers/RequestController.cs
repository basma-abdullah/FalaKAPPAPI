﻿using FalaKAPP.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static QRCodes.Controllers.QrCodeController;
using System.Data.SqlClient;
using System.Xml.Linq;
using static QRCoder.PayloadGenerator;

namespace FalaKAPP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RequestController : ControllerBase
    {
        [HttpPost]
        public ActionResult<LostNotificationRequest> addNewRequest(LostNotificationRequest request)
        {
            using (SqlConnection connection = new SqlConnection(DatabaseSettings.dbConn))
            {
                connection.Open();
                string query = "INSERT INTO LostNotificationRequest(RequestLostNotificationDate,requestTitle ,mainPersonInChargeID,ChildID,LastLocationId,NotificationStatus,Comments)VALUES(@RequestLostNotificationDate,@requestTitle , @mainPersonInChargeID,@ChildID,@LastLocationId,@NotificationStatus,@Comments)";
                SqlCommand comm = new SqlCommand(query, connection);
                comm.Parameters.AddWithValue("@RequestLostNotificationDate", request.RequestLostNotificationDate);
                comm.Parameters.AddWithValue("@requestTitle", request.requestTitle);
                comm.Parameters.AddWithValue("@mainPersonInChargeID", request.mainPersonInChargeID);
                comm.Parameters.AddWithValue("@ChildID", request.ChildID);
                comm.Parameters.AddWithValue("@LastLocationId", request.LastLocationId);
                comm.Parameters.AddWithValue("@NotificationStatus", request.NotificationStatus);
                comm.Parameters.AddWithValue("@Comments", request.Comments);

                int affectedrow = comm.ExecuteNonQuery();
                if(affectedrow > 0)
                {
                    return Ok("successfully created");
                }
                else
                {
                    return BadRequest("not created");
                }



            }       
        }

        //to return child information when click on specific child from list 
        [HttpGet ("GetChildInformationForRequest")]
        public ChildInformation GetChildInformationForRequest(int childId, int userId)
        {
            using (SqlConnection connection = new SqlConnection(DatabaseSettings.dbConn))
            {
                connection.Open();
                string query = $"SELECT c.ChildID, cn.FullName , c.MainImagePath , YEAR(GETDATE()) - c.YearOfBirth AS Age , p2.PhoneNumber , c.VoulnteerChildLocationID " +
                               $"FROM PersonUsers p,PersonUsers p2, PersonUsers cn, PersonChilds c, FollowChilds fc " +
                               $"WHERE c.ChildID = cn.UserID " +
                               $"AND c.ChildID = @childId " +
                               $"AND c.MainPersonInChargeID = @userId " +
                               $"AND p.userid = c.MainPersonInChargeID " +
                               $"AND fc.ChildId = @childId " +
                               $"AND c.MainPersonInChargeID = p2.UserID ";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@childId", childId);
                    command.Parameters.AddWithValue("@userId", userId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            //Retrieve the child information from the reader
                            var childID = reader.GetInt32(reader.GetOrdinal("ChildID"));
                            var fullName = reader.GetString(reader.GetOrdinal("FullName"));
                            var MainImagePath = reader.GetString(reader.GetOrdinal("MainImagePath"));
                            var Age = reader.GetInt32(reader.GetOrdinal("Age"));
                            var PhoneNumber = reader.GetInt32(reader.GetOrdinal("PhoneNumber"));
                            var VoulnteerChildLocationID = reader.GetInt32(reader.GetOrdinal("VoulnteerChildLocationID"));

                            return new ChildInformation
                            {
                                childID = childID,
                                FullName = fullName,
                                MainImagePath = MainImagePath,
                                Age = Age,
                                phoneNumber = PhoneNumber,
                                TrackingChildMasterID = VoulnteerChildLocationID
                     
                            };
                        }
                    }
                }
            }

            return null;
        }


        //to get children IDs and display result in request add list 
        [HttpGet("GetchildsID/{UserID}")]
        public ActionResult<object> GetchildsID(int UserID)
        {
            SqlConnection conn = new SqlConnection(DatabaseSettings.dbConn);
            conn.Open();


            string sql = "SELECT PC.ChildID, CN.FullName " +
                         "FROM PersonChilds PC " +
                         "JOIN PersonUsers PU ON PC.MainPersonInChargeID = PU.UserID " +
                         "JOIN PersonUsers CN ON PC.ChildID = CN.UserID " +
                         "WHERE PC.MainPersonInChargeID = @UserID";

            SqlCommand Comm = new SqlCommand(sql, conn);
            Comm.Parameters.AddWithValue("@UserID", UserID);

            SqlDataReader reader = Comm.ExecuteReader();

            List<object> childslist = new List<object>();
            while (reader.Read())
            {
                var child = new
                {
                    ChildID = reader.GetInt32(reader.GetOrdinal("ChildID")),
                    FullName = reader.GetString(reader.GetOrdinal("FullName")),
                };

                childslist.Add(child);
            }

            conn.Close();

            if (childslist.Count > 0)
            {
                return Ok(childslist);
            }

            return NotFound("no child found");
        }


        //this API will return all user id near child in 30 meter to send request to them 
        [HttpGet("usersNearChild/{childId}")]
        public ActionResult<IEnumerable<int>> GetUsersNearChild(int childId)
        {
            // Retrieve the child's location from the 'personchild' table
            using (SqlConnection connection = new SqlConnection(DatabaseSettings.dbConn))
            {
                connection.Open();
                string getChildLocationQuery = "SELECT Longitude, Latitude FROM PersonChilds WHERE ChildID = @childId";
                SqlCommand getChildLocationCommand = new SqlCommand(getChildLocationQuery, connection);
                getChildLocationCommand.Parameters.AddWithValue("@childId", childId);

                using (SqlDataReader reader = getChildLocationCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        double childLongitude = Convert.ToDouble(reader["Longitude"]);
                        double childLatitude = Convert.ToDouble(reader["Latitude"]);

                        // Find users near the child's location within 30 meters
                        string getUsersNearChildQuery = "SELECT UserID FROM PersonUsers WHERE SQRT(POWER(Longitude - @childLongitude, 2) + POWER(Latitude - @childLatitude, 2)) <= 0.0063"; // 0.0003 degrees is approximately 30 meters

                        SqlCommand getUsersNearChildCommand = new SqlCommand(getUsersNearChildQuery, connection);
                        getUsersNearChildCommand.Parameters.AddWithValue("@childLongitude", childLongitude);
                        getUsersNearChildCommand.Parameters.AddWithValue("@childLatitude", childLatitude);
                        reader.Close();
                        List<int> userIds = new List<int>();
                        using (SqlDataReader userReader = getUsersNearChildCommand.ExecuteReader())
                        {
                            while (userReader.Read())
                            {
                                int userId = Convert.ToInt32(userReader["UserID"]);
                                userIds.Add(userId);
                            }
                            userReader.Close();
                        }

                        return Ok(userIds);
                    }
                    else
                    {
                        return NotFound("Child not found");
                    }
                }
            }
        }


        [HttpGet("gethistoryrequest/{UserID}")]
        public ActionResult<Object> gethistoryrequest(int UserID)
        {
            using (SqlConnection connection = new SqlConnection(DatabaseSettings.dbConn))
            {
                connection.Open();
                string query = $"SELECT DISTINCT cn.FullName, LN.LostNotificationRequestID, LN.requestTitle " +
                               $"FROM LostNotificationRequest LN " +
                               $"JOIN PersonChilds c ON LN.ChildID = c.ChildID " +
                               $"JOIN PersonUsers cn ON c.ChildID = cn.UserID " +
                               $"JOIN PersonUsers p ON c.MainPersonInChargeID = p.UserID " +
                               $"JOIN FollowChilds fc ON fc.ChildId = c.ChildID " +
                               $"WHERE LN.MainPersonInChargeID = @UserID AND LN.NotificationStatus = 'received'";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserID", UserID);

                    SqlDataReader reader = command.ExecuteReader();

                    List<object> requestslist = new List<object>();
                    while (reader.Read())
                    {
                        var request = new
                        {

                            FullName = reader.GetString(reader.GetOrdinal("FullName")),
                            LostNotificationRequestID= reader.GetInt32(reader.GetOrdinal("LostNotificationRequestID")),
                            requestTitle = reader.GetString(reader.GetOrdinal("requestTitle")),
                        };

                        requestslist.Add(request);
                    }

                    connection.Close();

                    if (requestslist.Count > 0)
                    {
                        return Ok(requestslist);
                    }

                    return NotFound("no child found");

                }
            }
        }

        [HttpGet("getcurrentrequest/{UserID}")]
        public ActionResult<Object> getcurrentrequest(int UserID)
        {
            using (SqlConnection connection = new SqlConnection(DatabaseSettings.dbConn))
            {
                connection.Open();
                string query = $"SELECT DISTINCT cn.FullName, LN.LostNotificationRequestID, LN.requestTitle " +
                               $"FROM LostNotificationRequest LN " +
                               $"JOIN PersonChilds c ON LN.ChildID = c.ChildID " +
                               $"JOIN PersonUsers cn ON c.ChildID = cn.UserID " +
                               $"JOIN PersonUsers p ON c.MainPersonInChargeID = p.UserID " +
                               $"JOIN FollowChilds fc ON fc.ChildId = c.ChildID " +
                               $"WHERE LN.MainPersonInChargeID = @UserID AND LN.NotificationStatus != 'received'";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserID", UserID);

                    SqlDataReader reader = command.ExecuteReader();

                    List<object> requestslist = new List<object>();
                    while (reader.Read())
                    {
                        var request = new
                        {

                            FullName = reader.GetString(reader.GetOrdinal("FullName")),
                            LostNotificationRequestID = reader.GetInt32(reader.GetOrdinal("LostNotificationRequestID")),
                            requestTitle = reader.GetString(reader.GetOrdinal("requestTitle")),
                        };

                        requestslist.Add(request);
                    }

                    connection.Close();

                    if (requestslist.Count > 0)
                    {
                        return Ok(requestslist);
                    }

                    return NotFound("no child found");

                }
            }
        }



        [HttpGet("gethirequestDetail/{LostNotificationRequestID}")]
        public ActionResult<Object> getrequestDetail( int LostNotificationRequestID)
        {
            using (SqlConnection connection = new SqlConnection(DatabaseSettings.dbConn))
            {
                connection.Open();
                string query = $"SELECT DISTINCT cn.FullName, c.MainImagePath , LNR.NotificationStatus, LNR.RequestLostNotificationDate, YEAR(GETDATE()) - c.YearOfBirth AS Age, LNR.Comments, p2.PhoneNumber " +
                               $"FROM PersonUsers p " +
                               $"JOIN PersonChilds c ON p.UserID = c.ChildID " +
                               $"JOIN PersonUsers cn ON c.ChildID = cn.UserID " +
                               $"JOIN FollowChilds fc ON fc.ChildId = c.ChildID " +
                               $"JOIN TrackingChildMaster TM ON fc.LinkChildsID = TM.LinkChildsID " +
                               $"JOIN TrackingChildPlaceDetail TD ON TM.TrackingChildMasterID = TD.TrackingChildMasterID " +
                               $"JOIN LostNotificationRequest LNR ON LNR.TrackingChildMasterID = TM.TrackingChildMasterID " +
                               $"JOIN PersonUsers p2 ON c.MainPersonInChargeID = p2.UserID " +
                               $"WHERE LNR.LostNotificationRequestID = @LostNotificationRequestID";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@LostNotificationRequestID", LostNotificationRequestID);

                    SqlDataReader reader = command.ExecuteReader();

                    List<object> requestslist = new List<object>();
                    while (reader.Read())
                    {
                        var request = new
                        {

                            FullName = reader.GetString(reader.GetOrdinal("FullName")),
                            MainImagePath = reader.GetString(reader.GetOrdinal("MainImagePath")),
                            NotificationStatus = reader.GetString(reader.GetOrdinal("NotificationStatus")),
                            RequestLostNotificationDate = reader.GetDateTime(reader.GetOrdinal("RequestLostNotificationDate")).Date.ToString("yyyy-MM-dd"),
                            Age = reader.GetInt32(reader.GetOrdinal("Age")),
                            Comments = reader.GetString(reader.GetOrdinal("Comments")),
                            PhoneNumber = reader.GetInt32(reader.GetOrdinal("PhoneNumber")),
                        };

                        requestslist.Add(request);
                    }
                    
                    connection.Close();

                    if (requestslist.Count > 0)
                    {
                        return Ok(requestslist);
                    }

                    return NotFound("no child found");

                }
            }
        }




        [HttpPut]
        public IActionResult updaterequeststate(int LostNotificationRequestID , string NotificationStatus)
        {
            SqlConnection conn = new SqlConnection(DatabaseSettings.dbConn);
            {
                conn.Open();

                    string sql = "UPDATE LostNotificationRequest SET NotificationStatus = @NotificationStatus WHERE LostNotificationRequestID = @LostNotificationRequestID";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        // Add parameters and their values
                        cmd.Parameters.AddWithValue("@NotificationStatus", NotificationStatus);
                        cmd.Parameters.AddWithValue("@LostNotificationRequestID", LostNotificationRequestID);
                        int affectedRows = cmd.ExecuteNonQuery();
                        if (affectedRows > 0)
                        {
                            conn.Close();
                            return Ok("successfully updated");
                        }
                        else
                        {
                            return NotFound("Error not updated");
                        }
                    }

            }


        }





        //helping class
        public class ChildInformation
        {
            public int childID {  get; set; }
            public string FullName { get; set; }
            public string MainImagePath { get; set; }
            public int Age { get; set; }
            public int phoneNumber { get; set; }
            public int TrackingChildMasterID { get; set; }
            public int TrackingChilPlaceDetailId { get; set; }
        }



    }
}
