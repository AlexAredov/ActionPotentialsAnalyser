"""
Библиотека для анализа потенциалов действий кардиомиоцитов
"""

import atypic_cardio_analyzer as can
import os
import sys
import shutil
import warnings
import math
from concurrent.futures import ProcessPoolExecutor
import dask.dataframe as dd

# ----------------------------------------------------------------------------------------------


if __name__ == "__main__":
    C_sharp_data = sys.argv[1]
    # C_sharp_data = "D:/programming/projects/py/potentials/source/ten.txt"
    lines = C_sharp_data.split('\n')
    warnings.filterwarnings("ignore")

    file = lines[0]
    inp_alpha_threshold = float(lines[1].replace(',', '.'))
    inp_start_offset = int(lines[2])
    inp_refractory_period = int(lines[3])
    limit_rad = float(lines[4].replace(',', '.'))
    """
    file = C_sharp_data
    inp_alpha_threshold = 2.5
    inp_start_offset = 25
    inp_refractory_period = 10
    limit_rad = 350
    """
    separating_folder = "separated_APs"

    file_folder = os.path.dirname(file)  # получаем путь до папки перед файлом
    separating_path = os.path.join(file_folder, separating_folder).replace('\\',
                                                                           '/')  # добавляем separating_folder в конец

    time, voltage = can.preprocess(file, step=4)
    action_potentials = can.find_action_potentials(time, voltage, alpha=inp_alpha_threshold,
                                               offset=inp_start_offset, refractory_period=inp_refractory_period)

    # <
    # Сохраняем каждые ПД в папку и получаем временные интервалы
    intervals = can.save_aps_to_txt(separating_path, action_potentials, time, voltage)
    # >
    phase_4_speed_list = []
    phase_0_speed_list = []
    num_of_APs = []
    radius_list = []

    x_y_list = []
    x_list = []
    y_list = []

    x_y_offset_list = []
    x_offset_list = []
    y_offset_list = []

    for number, ap in enumerate(action_potentials):
        # <
        phase_4_speed, phase_0_speed = can.find_voltage_speed(ap, time, voltage)
        # >
        # <
        if math.isnan(phase_4_speed):
            phase_4_speed = can.replace_nan_with_nearest(phase_4_speed_list, number)
        if math.isnan(phase_0_speed):
            phase_0_speed = can.replace_nan_with_nearest(phase_0_speed_list, number)

        phase_4_speed = round(phase_4_speed, 3)
        phase_0_speed = round(phase_0_speed, 3)

        phase_4_speed_list.append(phase_4_speed)
        phase_0_speed_list.append(phase_0_speed)
        num_of_APs.append(number + 1)
        # >

        # <
        current_ap_time = time[ap['pre_start']:ap['end']]
        current_ap_voltage = voltage[ap['pre_start']:ap['end']]

        radius, x, y = can.circle(current_ap_time, current_ap_voltage, avr_rad=limit_rad)
        if math.isnan(radius):
            radius = can.replace_nan_with_nearest(radius_list, number)

        if math.isnan(x):
            x = can.replace_nan_with_nearest(radius_list, number)

        if math.isnan(y):
            y = can.replace_nan_with_nearest(radius_list, number)

        x_list.append(x)
        y_list.append(y)

        radius_list.append(round(radius, 3))
        x_y_list.append([round(x, 3), round(y, 3)])

        x_offset = time[ap['start']]
        y_offset = voltage[ap['start']]

        if math.isnan(x_offset):
            x_offset = can.replace_nan_with_nearest(x_y_offset_list, number)

        if math.isnan(y_offset):
            y_offset = can.replace_nan_with_nearest(x_y_offset_list, number)

        x_offset_list.append(x_offset)
        y_offset_list.append(y_offset)
        x_y_offset_list.append([round(x_offset, 3), round(y_offset, 3)])
        # >

    print(intervals)
    print(separating_path)
    print(phase_0_speed_list)
    print(phase_4_speed_list)
    print(num_of_APs)
    print(radius_list)
    print(x_y_list)
    print(x_y_offset_list)
