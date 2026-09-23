package com.lesliecleaner.android

sealed interface TargetIdentity {
    val stableKey: String

    data class PackageName(val value: String, val profileId: String = "default") : TargetIdentity {
        override val stableKey: String = "package:$profileId:$value"
        init { require(value.matches(Regex("[A-Za-z0-9_]+(\\.[A-Za-z0-9_]+)+"))) }
    }

    data class DocumentUri(val value: String) : TargetIdentity {
        override val stableKey: String = "document:$value"
        init { require(value.startsWith("content://")) }
    }

    data class MediaStoreId(val collection: String, val id: Long) : TargetIdentity {
        override val stableKey: String = "media:$collection:$id"
        init { require(collection.isNotBlank() && id > 0) }
    }

    data class AppOwnedRecord(val recordId: String) : TargetIdentity {
        override val stableKey: String = "record:$recordId"
        init { require(recordId.isNotBlank()) }
    }
}
