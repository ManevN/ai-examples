import os
import urllib.request
import re

file_path = 'the-verdict.txt'  # define this once so it's always available

if not os.path.exists(file_path):
    url = ("https://raw.githubusercontent.com/rasbt/LLMs-from-scratch/refs/heads/main/ch02/01_main-chapter-code/the-verdict.txt")
    urllib.request.urlretrieve(url, file_path)

with open(file_path, 'r', encoding="utf-8") as f:
    raw_text = f.read()

print(raw_text)  # if you actually want to see the text
print(len(raw_text))  # length of the text in characters

text = "Hello, world. This, it a test."
result = re.split(r'(\s)', text)
print(result)

result = re.split(r'([,.]|\s)', text)
print(result)

result = [item for item in result if item.strip()]
print(result)

text = "Hello, world. Is this-- a test?"
result = re.split(r'([,.:;?_!"()\']|--|\s)', text)
result = [item.strip() for item in result if item.strip()]
print(result)

result = re.split(r'([,.:;?_!"()\']|--|\s)', raw_text)
result = [item.strip() for item in result if item.strip()]
print(result)
