package com.lesliecleaner.android

import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class IdentityTest {
    @Test
    fun packageIdentityIsStableAndProfileScoped() {
        val identity = TargetIdentity.PackageName("com.example.cleaner", "work")
        assertEquals("package:work:com.example.cleaner", identity.stableKey)
    }

    @Test
    fun invalidPackageNameCannotBecomeMutationTarget() {
        assertThrows(IllegalArgumentException::class.java) {
            TargetIdentity.PackageName("Cleaner")
        }
    }

    @Test
    fun mediaIdentityRejectsNonPositiveIds() {
        assertThrows(IllegalArgumentException::class.java) {
            TargetIdentity.MediaStoreId("images", 0)
        }
    }
}
