import sys, os
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))
from cli import get_apiview_comments

# Try to get review comments for `azure-monitor-healthmodels`
comments = get_apiview_comments("d48830fcdd2c4713b6344d61ae626d5e")
print(comments)
