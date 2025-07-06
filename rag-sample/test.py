from sentence_transformers import SentenceTransformer


model = SentenceTransformer('all-MiniLM-L6-v2') # 384-dim output

sentences = ["The dog is barking.", "The cas is sleeping", "The cat is playing."]

embeddings = model.encode(sentences)

print(embeddings.shape)  # Should print (2, 384) for two sentences with 384-dimensional embeddings
print(embeddings[0])
print(len(embeddings[0]))