# Script verifies the operating system for the platform on which it is being run
# Used in build pipelines to verify the build agent os
# Variable: The name of the os to verfy against

import sys
import platform
osparameter = sys.argv[1]
print ("Parameter passed by matrix -", osparameter)
osparameter = 'Darwin' if(osparameter.lower() == 'MacOS'.lower()) else osparameter
agentos = platform.system()
if (agentos.lower().startswith(osparameter.lower()[:3])):
	print ('Job ran on %s OS' %agentos)
else:
	raise Exception('Job ran on the Wrong OS: %s' %agentos)