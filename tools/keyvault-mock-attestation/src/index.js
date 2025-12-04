const { initKeys } = require("./keys");
const { createServer } = require("./server");

const PORT = process.env.PORT || 5000;

async function main() {
  const { keyStore, privateKey, signingKeyId } = await initKeys();
  const server = createServer({
    keyStore,
    privateKey,
    signingKeyId,
    port: PORT,
  });

  await new Promise((resolve, reject) => {
    server.listen(PORT, (err) => {
      if (err) {
        reject(err);
      }

      console.log(`Server listening on port ${PORT}`);
      resolve();
    });
  });
}

main().catch(console.error);
