import lmdb
from discord import app_commands

class MissingAuthVerifier(app_commands.AppCommandError):
    pass

class MissingTokenMessage(app_commands.AppCommandError):
    pass

class KeyValDB:
    def __init__(self, dbPath: str):
        self.DbEnv = lmdb.open(dbPath, max_dbs=4, map_size=10 * 1024 * 1024)
        
        self.TokenMessageDB = self.DbEnv.open_db(b"TokenMessageDB")
        self.AuthVerifierDB = self.DbEnv.open_db(b"AuthVerifierDB")
        self.AuthMessageDB = self.DbEnv.open_db(b"AuthMessageDB")
        self.BulletExpDB = self.DbEnv.open_db(b"BulletExpDB")

    def Get(self, bucket, key, decode=True):
        with self.DbEnv.begin() as txn:
            result = txn.get(self._objToDB(key), db=bucket)

            if result is None:
                return None
            
            if decode:
                return result.decode()
            
            return result

    def Set(self, bucket, key, value, encode=True):
        with self.DbEnv.begin(write=True) as txn:
            putVal = self._objToDB(value) if encode else value
            txn.put(self._objToDB(key), putVal, db=bucket)

    def Del(self, bucket, key):
        with self.DbEnv.begin(write=True) as txn:
            txn.delete(self._objToDB(key), db=bucket)
    
    def Count(self, bucket):
        with self.DbEnv.begin() as txn:
            return txn.stat(db=bucket)["entries"]

    def _objToDB(self, obj):
        return bytes(str(obj), encoding="utf-8")