import lmdb

class KeyValDB:
    def __init__(self):
        self.DbEnv = lmdb.open("ORCA.db", max_dbs=3, map_size=10 * 1024 * 1024)
        
        self.TokenMessageDB = self.DbEnv.open_db(b"TokenMessageDB")
        self.AuthVerifierDB = self.DbEnv.open_db(b"AuthVerifierDB")
        self.AuthMessageDB = self.DbEnv.open_db(b"AuthMessageDB")

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
    
    def _objToDB(self, obj):
        return bytes(str(obj), encoding="utf-8")