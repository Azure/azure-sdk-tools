# Verifies Length of file path for all files in the SourceDirectory.
# Breaks if file path is 260 characters or more
import os
import sys

source_directory = sys.argv[1]
longest_path = ''
longest_path_length = 0

print('Analyzing length of paths...')
for root, dirs, files in os.walk('{0}'.format(source_directory)):
    for file in files:
        file_path = os.path.join(root,file)
        if (len(file_path) > longest_path_length):
            longest_path_length = len(file_path)
            longest_path = file_path
            if (longest_path_length >= 260):
                raise Exception('{0}  : is 260 or more Characters long. Reduce path length'.format(longest_path))
print('The Longest path {0} is {1} characters long'.format(longest_path, longest_path_length))