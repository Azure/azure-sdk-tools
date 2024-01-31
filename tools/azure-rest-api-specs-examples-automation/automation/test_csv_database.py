import os
import unittest
import shutil
from datetime import datetime
from os import path

from csv_database import CsvDatabase


class TestCsvDatabase(unittest.TestCase):

    def test(self):
        csv_release_content = '''id,name,language,tag,package,version,date_epoch,date
1,com.azure.resourcemanager:azure-resourcemanager-confluent:1.0.0-beta.3,java,azure-resourcemanager-confluent_1.0.0-beta.3,azure-resourcemanager-confluent,1.0.0-beta.3,1636608276,11/11/2021
2,com.azure.resourcemanager:azure-resourcemanager-signalr:1.0.0-beta.3,java,azure-resourcemanager-signalr_1.0.0-beta.3,azure-resourcemanager-signalr,1.0.0-beta.3,1636606853,11/11/2021
'''

        csv_file_content = '''id,file,release_id
1,specification/confluent/resource-manager/Microsoft.Confluent/preview/2021-09-01-preview/examples-java/MarketplaceAgreements_Create.java,1
2,specification/confluent/resource-manager/Microsoft.Confluent/preview/2021-09-01-preview/examples-java/MarketplaceAgreements_List.java,1
3,specification/confluent/resource-manager/Microsoft.Confluent/preview/2021-09-01-preview/examples-java/OrganizationOperations_List.java,1
4,specification/confluent/resource-manager/Microsoft.Confluent/preview/2021-09-01-preview/examples-java/Organization_Create.java,1
5,specification/confluent/resource-manager/Microsoft.Confluent/preview/2021-09-01-preview/examples-java/Organization_Delete.java,1
6,specification/confluent/resource-manager/Microsoft.Confluent/preview/2021-09-01-preview/examples-java/Organization_Get.java,1
7,specification/confluent/resource-manager/Microsoft.Confluent/preview/2021-09-01-preview/examples-java/Organization_ListByResourceGroup.java,1
8,specification/confluent/resource-manager/Microsoft.Confluent/preview/2021-09-01-preview/examples-java/Organization_ListBySubscription.java,1
9,specification/confluent/resource-manager/Microsoft.Confluent/preview/2021-09-01-preview/examples-java/Organization_Update.java,1
10,specification/confluent/resource-manager/Microsoft.Confluent/preview/2021-09-01-preview/examples-java/Validations_ValidateOrganizations.java,1
11,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/Operations_List.java,2
12,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalRPrivateEndpointConnections_Delete.java,2
13,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalRPrivateEndpointConnections_Get.java,2
14,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalRPrivateEndpointConnections_List.java,2
15,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalRPrivateEndpointConnections_Update.java,2
16,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalRPrivateLinkResources_List.java,2
17,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalRSharedPrivateLinkResources_CreateOrUpdate.java,2
18,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalRSharedPrivateLinkResources_Delete.java,2
19,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalRSharedPrivateLinkResources_Get.java,2
20,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalRSharedPrivateLinkResources_List.java,2
21,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_CheckNameAvailability.java,2
22,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_CreateOrUpdate.java,2
23,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_Delete.java,2
24,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_Get.java,2
25,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_ListByResourceGroup.java,2
26,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_ListBySubscription.java,2
27,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_ListKeys.java,2
28,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_ListSkus.java,2
29,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_RegenerateKey.java,2
30,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_Restart.java,2
31,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/SignalR_Update.java,2
32,specification/signalr/resource-manager/Microsoft.SignalRService/stable/2021-10-01/examples-java/Usages_List.java,2'''

        work_dir = path.abspath('.')

        index_file_path = path.join(work_dir, 'csvdb', 'java-library-example-index.csv')
        list_file_path = path.join(work_dir, 'csvdb', 'java-library-example-list.csv')

        shutil.rmtree(path.join(work_dir, 'csvdb'), ignore_errors=True)
        os.mkdir('csvdb')

        with open(index_file_path, 'w', newline='') as csv_file:
            csv_file.write(csv_release_content)

        with open(list_file_path, 'w', newline='') as csv_file:
            csv_file.write(csv_file_content)

        test_db = CsvDatabase(work_dir)
        test_db.load()

        self.assertEqual(2, len(test_db.release_db.rows))
        self.assertEqual(32, len(test_db.file_db.rows))

        self.assertEqual(3, test_db.release_db.next_id)
        self.assertEqual(33, test_db.file_db.next_id)

        releases = test_db.query_releases("java")
        self.assertEqual(2, len(releases))
        release1 = releases[0]
        self.assertEqual('azure-resourcemanager-confluent_1.0.0-beta.3', release1.tag)
        self.assertEqual('azure-resourcemanager-confluent', release1.package)
        self.assertEqual('1.0.0-beta.3', release1.version)

        test_db.new_release('com.azure.resourcemanager:azure-resourcemanager-quota:1.0.0-beta.2', 'java',
                            'azure-resourcemanager-quota_1.0.0-beta.2', 'azure-resourcemanager-quota',
                            '1.0.0-beta.2', datetime.fromtimestamp(1636603745),
                            ['specification/quota/resource-manager/Microsoft.Quota/preview/2021-03-15-preview/examples-java/GetOperations.java'])

        self.assertEqual(3, len(test_db.release_db.rows))
        self.assertEqual(33, len(test_db.file_db.rows))
        releases = test_db.query_releases("java")
        self.assertEqual(3, len(releases))

        test_db.dump()
        test_db = CsvDatabase(work_dir)
        test_db.load()
        releases = test_db.query_releases("java")
        self.assertEqual(3, len(releases))

        shutil.rmtree(path.join(work_dir, 'csvdb'), ignore_errors=True)
