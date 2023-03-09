import AwsKmsAccount from "../src/AwsKmsAccount";
import { AwsKmsKeyId } from "../src/AwsKmsKeyId";
import { AwsKmsKeyStore } from "../src/AwsKmsKeyStore";
import { parseSubjectPublicKeyInfo } from "../src/asn1";
import {
  CreateKeyCommand,
  DescribeKeyCommand,
  GetPublicKeyCommand,
  KMSClient,
  type KeyMetadata,
  ListKeysCommand,
  OriginType,
  ScheduleKeyDeletionCommand,
} from "@aws-sdk/client-kms";
import { PublicKey, Signature } from "@planetarium/account";
import { AggregateError } from "es-aggregate-error";
import { randomUUID } from "node:crypto";
import { hostname, userInfo } from "node:os";
import { inspect } from "node:util";
import { afterEach, describe, expect, test } from "vitest";

const envsConfigured =
  process.env.AWS_ACCESS_KEY_ID &&
  process.env.AWS_SECRET_ACCESS_KEY &&
  process.env.AWS_REGION;

interface FixtureKey {
  keyId: AwsKmsKeyId;
  publicKey: PublicKey;
  metadata: KeyMetadata;
  clean(): Promise<void>;
}

describe.runIf(envsConfigured)("AwsKmsKeyStore", async () => {
  const client = new KMSClient({});
  console.info("The below tests are run on the following AWS session:");
  console.table({
    AWS_ACCESS_KEY_ID: process.env.AWS_ACCESS_KEY_ID,
    AWS_REGION: process.env.AWS_REGION,
  });

  const listCmd = new ListKeysCommand({ Limit: 1000 });
  const keysList = await client.send(listCmd);
  const listWindow = Math.max((keysList.Keys ?? []).length / 2, 2);
  const testSessionId = randomUUID();
  const store = new AwsKmsKeyStore(client, {
    listWindow,
    scopingTags: { TestSessionId: testSessionId },
  });
  const keyDescription = `Auto-generated by @planetarium/account-aws-kms unit tests; ran inside ${
    userInfo().username
  }@${hostname()}`;

  async function createFixtureKey(): Promise<FixtureKey> {
    const cmd = new CreateKeyCommand({
      KeySpec: "ECC_SECG_P256K1",
      KeyUsage: "SIGN_VERIFY",
      Description: keyDescription,
      MultiRegion: false,
      Tags: [{ TagKey: "TestSessionId", TagValue: testSessionId }],
    });
    const response = await client.send(cmd);
    if (response.KeyMetadata == null) throw new Error("Failed to generate key");
    const keyId = response.KeyMetadata.KeyId;
    if (keyId == null) throw new Error("Failed to generate key");

    let alreadyDeleted = false;
    async function clean(): Promise<void> {
      if (alreadyDeleted) return;
      const delCmd = new ScheduleKeyDeletionCommand({
        KeyId: keyId,
        PendingWindowInDays: 7,
      });
      try {
        await client.send(delCmd);
      } catch (_) {}
      alreadyDeleted = true;
    }

    try {
      const pubKeyCmd = new GetPublicKeyCommand({ KeyId: keyId });
      const pubKeyResp = await client.send(pubKeyCmd);
      if (pubKeyResp.PublicKey == null) {
        throw new Error("Failed to get public key");
      }
      const publicKeyBytes: Uint8Array = parseSubjectPublicKeyInfo(
        pubKeyResp.PublicKey,
      );
      const publicKey = PublicKey.fromBytes(publicKeyBytes, "uncompressed");
      return { keyId, publicKey, metadata: response.KeyMetadata, clean };
    } catch (e) {
      await clean();
      throw e;
    }
  }

  async function createFixtureKeys(
    number: number,
  ): Promise<[Record<AwsKmsKeyId, PublicKey>, () => Promise<void>]> {
    const promises: Promise<FixtureKey>[] = [];
    for (let i = 0; i < number; i++) {
      promises.push(createFixtureKey());
    }
    const keys: FixtureKey[] = [];
    const errors: Error[] = [];
    for (const promise of promises) {
      try {
        keys.push(await promise);
      } catch (e) {
        errors.push(e);
      }
    }

    async function cleanAll(): Promise<void> {
      await Promise.all(keys.map((triple) => triple.clean()));
    }

    if (errors.length > 0) {
      await cleanAll();
      throw new AggregateError(errors);
    }

    return [
      Object.fromEntries(
        keys.map((triple) => [triple.keyId, triple.publicKey]),
      ),
      cleanAll,
    ];
  }

  test("list", async () => {
    let i = 0;
    for await (const _ of store.list()) i++;
    expect(i).toBeFalsy();

    const before = new Date();
    const [expectedKeys, cleanFixtures] = await createFixtureKeys(5);
    const after = new Date();
    afterEach(cleanFixtures);

    const fetched: Set<AwsKmsKeyId> = new Set();
    for await (const key of store.list()) {
      expect(key.createdAt).toBeDefined();
      if (key.createdAt == null) throw new Error(); // type guard
      expect(+key.createdAt).toBeGreaterThanOrEqual(+before - 1000);
      expect(+key.createdAt).toBeLessThanOrEqual(+after + 1000);
      fetched.add(key.keyId);
    }

    expect(fetched).toStrictEqual(new Set(Object.keys(expectedKeys)));
  });

  test("get", async () => {
    const result = await store.get("00000000-0000-0000-0000-000000000000");
    expect(result).toStrictEqual({
      result: "keyNotFound",
      keyId: "00000000-0000-0000-0000-000000000000",
    });

    const { keyId, publicKey, metadata, clean } = await createFixtureKey();
    afterEach(clean);
    const account = new AwsKmsAccount(keyId, publicKey, client);

    const result2 = await store.get(keyId);
    expect(result2).toStrictEqual({
      result: "success",
      keyId,
      account,
      metadata: {
        customKeyStoreId: metadata.CustomKeyStoreId,
        description: metadata.Description ?? "",
        multiRegion: metadata.MultiRegion ?? false,
        origin: metadata.Origin ?? OriginType.AWS_KMS,
      },
      createdAt: metadata.CreationDate,
    });
    expect(account.publicKey.toHex("compressed")).toStrictEqual(
      publicKey.toHex("compressed"),
    );

    const msg = new Uint8Array([0x01, 0x02, 0x03]);
    const sig = await account.sign(msg);
    const verified = await publicKey.verify(msg, sig);
    if (!verified) {
      console.log({ msg, sig, publicKey: publicKey.toHex("compressed") });
    }
    expect(verified).toStrictEqual(true);
  });

  test("generate", async () => {
    const result = await store.generate({
      description: keyDescription,
      multiRegion: false,
      origin: "AWS_KMS",
    });

    if (result.result === "success") {
      let alreadyDeleted = false;
      afterEach(async () => {
        if (alreadyDeleted) return;
        const delCmd = new ScheduleKeyDeletionCommand({
          KeyId: result.keyId,
          PendingWindowInDays: 7,
        });
        await client.send(delCmd);
        alreadyDeleted = true;
      });
    } else throw new Error(`failed to generate: ${inspect(result)}`); // type guard

    expect(result.result).toBe("success");
    const descCmd = new DescribeKeyCommand({ KeyId: result.keyId });
    const resp = await client.send(descCmd);
    expect(resp.KeyMetadata).toBeDefined();
    expect(resp.KeyMetadata?.KeyId).toMatch(
      new RegExp(`^${result.keyId}$`, "i"),
    );
  });

  test("delete", async () => {
    const result = await store.delete("00000000-0000-0000-0000-000000000000");
    expect(result).toStrictEqual({
      result: "keyNotFound",
      keyId: "00000000-0000-0000-0000-000000000000",
    });

    const { keyId, clean } = await createFixtureKey();
    afterEach(clean);

    const result2 = await store.delete(keyId);
    expect(result2).toStrictEqual({ result: "success", keyId });

    const cmd = new DescribeKeyCommand({ KeyId: keyId });
    const resp = await client.send(cmd);
    expect(resp.KeyMetadata?.Enabled).toBeFalsy();

    const result3 = await store.delete(keyId);
    expect(result3).toStrictEqual({ result: "keyNotFound", keyId });
  });
});
