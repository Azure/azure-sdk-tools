# Verifies Length of file path for all files in the SourceDirectory.
# Breaks if file path is 260 characters or more
import os
import sys

source_directory = sys.argv[1]
longest_file_path = ''
longest_file_path_length = 0
longest_dir_path = ''
longest_dir_path_length = 0

print('Analyzing length of paths...')
for root, dirs, files in os.walk('{0}'.format(source_directory)):
    for file in files:
        file_path = os.path.join(root, file)
        if (len(file_path) > longest_file_path_length):
            longest_file_path_length = len(file_path)
            longest_file_path = file_path
            if (longest_file_path_length >= 260):
                raise Exception('{0} : is 260 or more Characters long. Reduce path length'.format(longest_file_path))
        if (len(root) > longest_dir_path_length):
            longest_dir_path_length = len(root)
            longest_dir_path = root
            if (longest_dir_path_length >= 248):
                raise Exception('{0} : is 248 or more Characters long. Reduce path length'.format(root))
print('The Longest file path {0} is {1} characters long'.format(longest_file_path, longest_file_path_length))
print('The Longest directory path {0} is {1} characters long'.format(longest_dir_path, longest_dir_path_length))