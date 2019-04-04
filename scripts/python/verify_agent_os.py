# Script verifies the operating system for the platform on which it is being run
# Used in build pipelines to verify the build agent os
# Variable: The name of the os to verfy against

import sys
import platform
os_parameter = sys.argv[1]
print ("Parameter passed by matrix -", os_parameter)
os_parameter = 'Darwin' if(os_parameter.lower() == 'MacOS'.lower()) else os_parameter
agent_os = platform.system()
if (agent_os.lower() == os_parameter.lower()):
	print ('Job ran on %s OS' %agent_os)
else:
	raise Exception('Job ran on the Wrong OS: %s' %agent_os)