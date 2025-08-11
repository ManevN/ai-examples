import os
import urllib.request
import re

file_path = 'the-verdict.txt'

if not os.path.exists(file_path):
    url = ("https://raw.githubusercontent.com/rasbt/LLMs-from-scratch/refs/heads/main/ch02/01_main-chapter-code/the-verdict.txt")
    urllib.request.urlretrieve(url, file_path)

with open(file_path, 'r', encoding="utf-8") as f:
    raw_text = f.read()

print(len(raw_text))

# Preprocess raw_text
result = re.split(r'([,.:;?_!"()\']|--|\s)', raw_text)
result = [item.strip() for item in result if item.strip()]
preprocessed = result
print("First 10 tokens:", preprocessed[:10])

# Vocabulary
all_words = sorted(set(preprocessed))
vocab_size = len(all_words)
print("Vocab size:", vocab_size)

vocab = {token: integer for integer, token in enumerate(all_words)}
print("Sample vocab entries:", list(vocab.items())[:10])


class SimpleTokenizerV1:
    def __init__(self, vocab):
        self.str_to_int = vocab
        self.int_to_str = {i: s for s, i in vocab.items()}
    
    def encode(self, text):
        preprocessed = re.split(r'([,.:;?_!"()\']|--|\s)', text)  # match preprocessing
        preprocessed = [item.strip() for item in preprocessed if item.strip()]
        ids = [self.str_to_int[s] for s in preprocessed]
        return ids

    def decode(self, ids):
        text = " ".join([self.int_to_str[i] for i in ids])
        text = re.sub(r'\s+([,.?!"()\'])', r'\1', text)
        return text


tokenizer = SimpleTokenizerV1(vocab)

text = "Hello, world. Is this-- a test?"
ids = tokenizer.encode(text)
print("Encoded:", ids)

decoded = tokenizer.decode(ids)
print("Decoded:", decoded)
