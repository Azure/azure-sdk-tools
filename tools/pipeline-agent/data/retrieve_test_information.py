# The data available in the azure sdk kusto cluser related to TestRuns is quite limited,
# but an illustration of what is available is available here:
# https://dataexplorer.azure.com/clusters/azsdkengsys.westus2/databases/Pipelines?query=H4sIAAAAAAAAA12NMQ7CMBAEe15xJUipUARVKMANbcgHjG8hRo4dnc9CSDwep0GCemZ2B2TtS1y96TlCQMfiAxvcfPTqUzwzdR1t23b3NU5pmgMUbKyCDmTvab3nTeVJGELX15/CyK7SWdIDTmnwGtBQPTVF7HJygUuRc/Pb1UQQl0X1E9xoRT928CkvrQAAAA==
#
# Navigate to : https://dataexplorer.azure.com/clusters/azsdkengsys.westus2/databases/Pipelines and invoke
# TestRun
# | where BuildDefinitionId == 2446
# | where CompletedDate > ago(7d)
# | order by CompletedDate desc
# | project Title, RunDurationSeconds, CompletedDate
# | render timechart
#
#
#
# Because the test run is super high level, we need to reach out to the devs api to retrieve the actual historical build results.
# How far back can we actually go?


