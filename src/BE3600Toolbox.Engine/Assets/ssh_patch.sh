#!/bin/sh

DONE_FLAG="/tmp/ssh_patch.log"
LOCK_DIR="/tmp/ssh_patch.lock"

run_patch() {
	# 1. If patch was already successfully applied in this session, exit immediately
	[ -e "$DONE_FLAG" ] && return 0

	# 2. Prevent concurrent execution: exit immediately if another instance is running
	mkdir "$LOCK_DIR" 2>/dev/null || return 0
	trap 'rm -rf "$LOCK_DIR"' EXIT INT TERM

	SSH_EN=`nvram get ssh_en`
	if [ "$SSH_EN" != "1" ]; then
		nvram set ssh_en=1
		nvram commit
	fi

	if grep -q '= "release"' /etc/init.d/dropbear ; then
		sed -i 's/= "release"/= "XXXXXX"/g' /etc/init.d/dropbear
	fi

	/etc/init.d/dropbear enable
	/etc/init.d/dropbear restart

	# 3. Mark as successfully completed in this session
	echo "ssh enabled" > "$DONE_FLAG"
	return 0
}

run_patch
