using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Security;
using PanoptoClientSoapiness.ServiceReference1;

namespace PanoptoClientSoapiness
{
    class Program
    {
        private static int maxPerPage = 10;

        private static void Main(string[] args)
        {
            var hosts = new[] { 
                new { ConfigName = "testing", Description = "Via SLB and ELB" }                
            };
            
            foreach (var host in hosts)
            {
                Console.WriteLine("Testing: {0} ({1})", host.Description, host.ConfigName);

                try
                {
                    SessionManagementClient smc = new SessionManagementClient();
                    AuthenticationInfo auth = new AuthenticationInfo();

                    // Use an account that is a member of groups that have the right permissions.
                    auth.Password = "testPassword";
                    auth.UserKey = "testAccount";
                    auth.AuthCode = String.Empty;

                    // Names of folders to scan
                    string[] folderNames = new string[] { "One Folder", "Some other folder name" };

                    List<Session> sessions = new List<Session>();

                    Pagination pagination = new Pagination { PageNumber = 0, MaxNumberResults = maxPerPage };
                    ListFoldersRequest request = new ListFoldersRequest { Pagination = pagination };

                    // Get all the folders we need to index
                    foreach (string folderName in folderNames)
                    {
                        ListFoldersResponse response = smc.GetFoldersList(auth, request, folderName);
                        foreach (Folder folder in response.Results)
                        {
                            // Confirm we found a folder with the exact right name, then add.
                            if (folder.Name.ToLower() == folderName.ToLower())
                            {
                                Console.WriteLine("\tSearching folder: " + folderName);
                                sessions.AddRange(searchForSessions(auth, smc, folder));
                            }
                        }
                    }

                    if (sessions.Count > 0)
                    {
                        string gsaStream = sessionsAsGsaStream(sessions);

                        Console.WriteLine("\tAll sessions: \n" + gsaStream);
                    }
                    else
                    {
                        Console.WriteLine("\tFound zero sessions.");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("\tError: " + e.Message);
                }

                Console.WriteLine();
            }

            Console.WriteLine("\nCompleted");
            Console.ReadLine();
        }

        private static List<Session> searchForSessions(AuthenticationInfo auth, SessionManagementClient smc, Folder folder)
        {
            List<Session> sessionsToIndex = new List<Session>();
            try
            { 
                // Search for all sessions in this folder
                Guid folderId = folder.Id;

                Pagination pagination = new Pagination { PageNumber = 0, MaxNumberResults = maxPerPage };
                ListSessionsRequest sessionRequest = new ListSessionsRequest { Pagination = pagination, FolderId = folderId};
                ListSessionsResponse response = smc.GetSessionsList(auth, sessionRequest, "");

                if (response.TotalNumberResults == 0)
                {
                    Console.WriteLine("Found 0 results.");
                }
                else
                {
                    int pagesOfResults = response.TotalNumberResults / maxPerPage;
                    
                    // List the sessions from the initial request
                    foreach (Session session in response.Results)
                    {
                        // Add sessions to the list.
                        sessionsToIndex.Add(session);
                    }

                    // If there are more pages, make additional network requests
                    for (int page = 1; page < pagesOfResults; page++)
                    {
                        pagination = new Pagination { PageNumber = page, MaxNumberResults = maxPerPage };
                        sessionRequest = new ListSessionsRequest { Pagination = pagination, FolderId = folderId };
                        response = smc.GetSessionsList(auth, sessionRequest, "");

                        // List the sessions from the initial request
                        foreach (Session session in response.Results)
                        {
                            // Add sessions to the list.
                            sessionsToIndex.Add(session);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Error while searching for sessions in folder: {0} - {1}", folder.Name, e.Message));
            }
            return sessionsToIndex;
        }

        private static string sessionsAsGsaStream(List<Session> sessions)
        {
            // Header for GSA
            string gsaDataToReturn = "<?xml version=\"1.0\" encoding=\"UTF8\"?><!DOCTYPE gsafeed PUBLIC \"-//Google//DTD GSA Feeds//EN\" \"\"><gsafeed><header><datasource>Panopto</datasource><feedtype>full</feedtype></header><group>";

            // Format for basic inclusion of Panopto data
            string sessionFormatString = "<record url=\"{0}\" mimetype=\"text/plain\" lock=\"true\"><content>{1}</content></record>";
            foreach (Session session in sessions)
            {
                // Add each session
                gsaDataToReturn += String.Format(sessionFormatString, session.ViewerUrl, session.Name);
            }

            //Close tags for GSA
            gsaDataToReturn += "</group></gsafeed>";
            return gsaDataToReturn;
        }
    }
}
