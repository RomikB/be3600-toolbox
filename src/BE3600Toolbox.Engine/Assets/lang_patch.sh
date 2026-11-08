#!/bin/sh

DONE_FLAG=/tmp/lang_patch.log
LOCK_DIR=/tmp/lang_patch.lock

DIR_PATCH=/etc/crontabs/patches

TARGET1_DIR=/usr/share/xiaoqiang
MIRROR1_DIR=/tmp/_usr_share_xiaoqiang

TARGET2_DIR=/usr/lib/lua/luci
MIRROR2_DIR=/tmp/_usr_lib_lua_luci

run_patch() {
	# 1. If patch was already successfully applied in this session, exit immediately
	[ -e "$DONE_FLAG" ] && return 0

	# 2. Prevent concurrent execution: exit immediately if another instance is running
	mkdir "$LOCK_DIR" 2>/dev/null || return 0
	trap 'rm -rf "$LOCK_DIR"' EXIT INT TERM

	if [ `find $DIR_PATCH -maxdepth 1 -name 'base.*.lmo' | wc -l` -eq 0 ]; then
		return 0
	fi

	# Patch target 1: /usr/share/xiaoqiang
	if ! mount | grep -q " on $TARGET1_DIR" ; then
		mkdir -p $MIRROR1_DIR
		cp -rf $TARGET1_DIR/* $MIRROR1_DIR/
		mount --bind $MIRROR1_DIR $TARGET1_DIR
	fi
	if ! mount | grep -q " on $TARGET1_DIR" ; then
		return 1  # error
	fi
	if [ ! -f $MIRROR1_DIR/xiaoqiang_version ]; then
		return 1  # error
	fi

	# unlock change luci.main.lang
	sed -i "s/option CHANNEL 'stable'/option CHANNEL 'release'/g" $TARGET1_DIR/xiaoqiang_version

	# Patch target 2: /usr/lib/lua/luci
	if ! mount | grep -q " on $TARGET2_DIR" ; then
		mkdir -p $MIRROR2_DIR
		cp -rf $TARGET2_DIR/* $MIRROR2_DIR/
		mount --bind $MIRROR2_DIR $TARGET2_DIR
	fi
	if ! mount | grep -q " on $TARGET2_DIR" ; then
		return 1  # error
	fi
	if [ ! -f $MIRROR2_DIR/i18n.lua ]; then
		return 1  # error
	fi

	cp -f $DIR_PATCH/base.*.lmo /usr/lib/lua/luci/i18n/

	# unlock WEB lang menu
	sed -i 's/ and features\["system"\]\["i18n"\] == "1" //' /usr/lib/lua/luci/view/web/inc/sysinfo.htm

	MAIN_LANG=$( uci -q get luci.main.lang )
	[ "$MAIN_LANG" = "" ] && uci set luci.main.lang=en
	uci set luci.languages.ru=Русский
	uci set luci.languages.en=English
	uci commit luci

	# reload luci
	luci-reload
	rm -f /tmp/luci-indexcache
	luci-reload

	# 3. Mark as successfully completed in this session
	echo "lang patched" > "$DONE_FLAG"
	return 0
}

run_patch
