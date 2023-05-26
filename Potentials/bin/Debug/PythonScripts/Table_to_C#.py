"""
Библиотека для анализа потенциалов действий кардиомиоцитов
"""

import numpy as np
import matplotlib.pyplot as plt
import pandas as pd
import openpyxl
import os
from scipy.ndimage import gaussian_filter
from io import StringIO
from skimage.restoration import denoise_tv_chambolle
from math import sqrt
from itertools import islice
from openpyxl.utils import get_column_letter
import sys
import shutil
import warnings
import math
from concurrent.futures import ProcessPoolExecutor
import dask.dataframe as dd


def open_txt(file):
    """
    Открывает текстовый файл и возвращает массивы времени и напряжения, разделенных табуляцией.

    Аргументы:
        file (str): Путь к текстовому файлу.

    Возвращает:
        tuple: Кортеж из двух массивов numpy (первый столбец данных, второй столбец данных).
    """
    column_names = ['time', 'voltage']
    column_types = [np.float64, np.float64]

    try:
        # Читаем данные
        data = dd.read_csv(file, engine='c', sep='\t', decimal=',', encoding='latin-1',
                           on_bad_lines='skip', dtype=dict(zip(column_names, column_types)),
                           header=None, names=column_names)

        # Обрабатывайте данные
        data = data.compute()
        time = data['time'].values
        voltage = data['voltage'].values

        return time, voltage

    except ValueError as e:
        # Читаем данные
        data = dd.read_csv(file, engine='c', sep='\t', decimal='.', encoding='latin-1',
                           on_bad_lines='skip', dtype=dict(zip(column_names, column_types)),
                           header=None, names=column_names)

        # Обрабатывайте данные
        data = data.compute()
        time = data['time'].values
        voltage = data['voltage'].values

        return time, voltage


def preprocess(file, smooth=5):
    """
    Предварительная обработка данных из файла.

    Аргументы:
        file (str): Путь к текстовому файлу с исходными данными.
        smooth (int, optional): Параметр сглаживания для алгоритма denoise_tv_chambolle.
            По умолчанию равен 15.

    Возвращает:
        tuple: Кортеж из двух массивов numpy (время и напряжение).
    """
    time, voltage = open_txt(file)

    time = time[::4] * 1000
    voltage = voltage[::4] * 1000
    # voltage = denoise_tv_chambolle((voltage * 1000), smooth)
    voltage = gaussian_filter(voltage, smooth)

    return time, voltage


def find_action_potentials(time, voltage, alpha_threshold=25, refractory_period=10, start_offset=80):
    """
    Находит ПД в данных времени и напряжения.

    Функция анализирует производную напряжения по времени, выявляет начало фазы 0,
    пик и конец фазы 4 для каждого потенциала действия. Возвращает список словарей с
    индексами ключевых точек для каждого ПД.

    Аргументы:
        time (np.ndarray): 1D массив numpy содержащий значения времени.
        voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.
        alpha_threshold (float, optional): Пороговое значение производной напряжения
            для определения начала фазы 0. По умолчанию равен 25.
        refractory_period (int, optional): Рефрактерный период между значениями напряжения.
            По умолчанию равен 10.
        start_offset (int, optional): Смещение для определения начала фазы 0.
            По умолчанию равно 80.

    Возвращает:
        list: Список словарей с индексами ключевых точек для каждого ПД.
    """
    voltage_derivative = np.diff(voltage) / np.diff(time)

    candidate_phase_0_start_indices = np.where(voltage_derivative > alpha_threshold)[0]
    diff_candidates = np.diff(candidate_phase_0_start_indices)

    phase_0_start_indices = np.insert(candidate_phase_0_start_indices[1:][diff_candidates > refractory_period], 0,
                                      candidate_phase_0_start_indices[0])

    action_potentials = []
    for i, start_index in enumerate(phase_0_start_indices):
        pre_start_index = action_potentials[-1]['end'] if i > 0 else 0

        if i < len(phase_0_start_indices) - 1:
            next_start_index = phase_0_start_indices[i + 1]
            peak_index = np.argmax(voltage[start_index:next_start_index]) + start_index
            end_index = np.argmin(voltage[peak_index:next_start_index]) + peak_index
        else:
            peak_index = np.argmax(voltage[start_index:]) + start_index
            end_index = np.argmin(voltage[peak_index:]) + peak_index

        action_potentials.append({
            'pre_start': pre_start_index,
            'start': start_index - start_offset,
            'peak': peak_index,
            'end': end_index
        })

    return action_potentials


def find_voltage_speed(ap, time, voltage):
    """
    Находит среднюю скорость изменения напряжения для фаз 4 и 0 ПД.

    Аргументы:
    ap (dict): Словарь с индексами ключевых точек ПД.
    time (np.ndarray): 1D массив numpy содержащий значения времени.
    voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.

    Возвращает:
    tuple: Кортеж из двух чисел - средней скорости изменения напряжения для фазы 4 и фазы 0.
    """
    prestart_index = ap['pre_start']
    start_index = ap['start']
    peak_index = ap['peak']

    phase_4_time = time[prestart_index:start_index]
    phase_4_voltage = voltage[prestart_index:start_index]
    phase_4_speed = np.diff(phase_4_voltage) / np.diff(phase_4_time)

    phase_0_time = time[start_index:peak_index]
    phase_0_voltage = voltage[start_index:peak_index]
    phase_0_speed = np.diff(phase_0_voltage) / np.diff(phase_0_time)

    return 1000 * np.mean(phase_4_speed), np.mean(phase_0_speed)


def circle(time, voltage, avr_rad):
    def nearest_value(items_x, items_y, value_x, value_y):
        dist = np.sqrt((items_x - value_x) ** 2 + (items_y - value_y) ** 2)
        return np.argmin(dist)

    def flat(x, y, n):
        return x[::n], y[::n]

    def radius(x_c, y_c, x_1, y_1, x_2, y_2):
        cent1_x, cent1_y = (x_c + x_1) / 2, (y_c + y_1) / 2
        cent2_x, cent2_y = (x_c + x_2) / 2, (y_c + y_2) / 2

        k1 = (cent1_x - x_c) / (cent1_y - y_c)
        b1 = cent1_y + k1 * cent1_x

        k2 = (cent2_x - x_c) / (cent2_y - y_c)
        b2 = cent2_y + k2 * cent2_x

        x_r = (b2 - b1) / (k2 - k1)
        y_r = -k1 * x_r + b1

        rad = np.sqrt((x_r - x_c) ** 2 + (y_r - y_c) ** 2)
        return rad, x_r, y_r

    x = np.array(time)
    y = np.array(voltage)

    x = x[:np.argmax(y)]
    y = y[:np.argmax(y)]

    l = 8
    dff = flat(x, y, l)
    ma = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))

    o = 10

    while ma + o >= len(dff[0]):
        l = l // 2
        if l == 0:
            return 10, -10, 0

        dff = flat(x, y, l)
        ma = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))

    while dff[1][ma + o] - dff[1][ma] >= 8:
        if (dff[1][ma + o] - dff[1][ma]) / (dff[1][ma + (o - 1)] - dff[1][ma]) > 3.5:
            o -= 1
            ma = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))
            break
        o -= 1
        ma = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))

    rad, x_r, y_r = radius(dff[0][ma], dff[1][ma], dff[0][ma + o], dff[1][ma + o], dff[0][ma - o], dff[1][ma - o])
    k = 0
    try:
        while rad > avr_rad:
            k += 1
            ma -= 10
            rad_new, x_r_new, y_r_new = radius(dff[0][ma], dff[1][ma], dff[0][ma + o], dff[1][ma + o], dff[0][ma - o],
                                               dff[1][ma - o])
            if k > 10000:
                break
        return rad_new, x_r_new, y_r_new

    except Exception as e:
        return rad, x_r, y_r


def save_aps_to_txt(destination, aps, time, voltage):
    """
    Сохраняет ПД в текстовый файл в определенную папку.

    Аргументы:
        destination (str): Путь к папке для сохранения.
        aps (list): Список словарей, содержащих ключи 'pre_start' и 'end'.
        time (np.ndarray): 1D массив numpy содержащий значения времени.
        voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.

    Возвращает:
        list: Лист временных интервалов для начала и конца каждого ПД
    """
    if os.path.exists(destination):
        shutil.rmtree(destination)

    if not os.path.exists(destination):
        os.makedirs(destination)

    time_intervals = []

    for number, ap in enumerate(aps):
        current_time = time[ap['pre_start']:ap['end']]
        current_voltage = voltage[ap['pre_start']:ap['end']]

        time_intervals.append([round(current_time[0], 3), round(current_time[-1], 3)])

        with open(os.path.join(destination, f"{number + 1}.txt"), 'w') as f:
            for i in range(current_time.shape[0]):
                f.write(f"{current_time[i]}\t{current_voltage[i]}\n")

    return time_intervals


def replace_nan_with_nearest(value_list, index):
    left, right = index - 1, index + 1

    try:
        while left >= 0 or right < len(value_list):
            if left >= 0 and not math.isnan(value_list[left]):
                return value_list[left]
            elif right < len(value_list) and not math.isnan(value_list[right]):
                return value_list[right]
            left -= 1
            right += 1
    finally:
        return 0


def save_aps_to_xlsx(destination, aps, time, voltage, limit_rad):
    """
    Сохраняет информацию о ПД в .xlsx таблицу.

    Аргументы:
        destination (str): Путь к папке для сохранения.
        aps (list): Список словарей, содержащих ключи 'pre_start' и 'end'.
        time (np.ndarray): 1D массив numpy содержащий значения времени.
        voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.

    Возвращает:
        None
    """
    data = []
    radius_list_local = []
    phase_4_speed_list_local = []
    phase_0_speed_list_local = []

    for number, ap in enumerate(aps):
        phase_4_speed, phase_0_speed = find_voltage_speed(ap, time, voltage)

        if math.isnan(phase_4_speed):
            phase_4_speed = replace_nan_with_nearest(phase_4_speed_list_local, number)
        if math.isnan(phase_0_speed):
            phase_0_speed = replace_nan_with_nearest(phase_0_speed_list_local, number)

        current_ap_time = time[ap['pre_start']:ap['end']]
        current_ap_voltage = voltage[ap['pre_start']:ap['end']]

        radius, x, y = circle(current_ap_time, current_ap_voltage, limit_rad)

        if math.isnan(radius):
            radius = replace_nan_with_nearest(radius_list_local, number)

        radius_list_local.append(radius)

        # radius, _, _ = circle(current_ap_time, current_ap_voltage)

        row = {
            "Номер ПД": number + 1,
            "Начало": f"{current_ap_time[0]:.2f}",
            "Конец": f"{current_ap_time[-1]:.2f}",
            "Радиус": round(radius, 2),
            "dV/dt4": round(phase_4_speed, 3),
            "dV/dt0": round(phase_0_speed, 3)
        }
        data.append(row)

    df = pd.DataFrame(data)
    df.to_excel(destination, index=False, engine='openpyxl')


# ----------------------------------------------------------------------------------------------

if __name__ == "__main__":
    C_sharp_data = sys.argv[1]
    # C_sharp_data = "D:/programming/projects/py/source/long_sample_data2019.txt"

    lines = C_sharp_data.split('\n')
    destination = lines[0]
    file_path = lines[1]

    inp_alpha_threshold = float(lines[2].replace(',', '.'))
    inp_start_offset = int(lines[3])
    inp_refractory_period = int(lines[4])
    limit_rad = float(lines[5].replace(',', '.'))
    """
    file_path = C_sharp_data
    inp_alpha_threshold = 0.9
    inp_start_offset = 0
    inp_refractory_period = 280
    limit_rad = 250
    destination = "D:/programming/projects/2019long.xlsx"
    """
    warnings.filterwarnings("ignore")

    phase_4_speed_list = []
    phase_0_speed_list = []
    num_of_APs = []
    radius_list = []

    # time, voltage = open_txt(file_path)
    time, voltage = preprocess(file_path)

    # Резка ПД
    action_potentials = find_action_potentials(time, voltage, alpha_threshold=inp_alpha_threshold,
                                               start_offset=inp_start_offset, refractory_period=inp_refractory_period)

    # Сохранение в табличку
    try:
        save_aps_to_xlsx(destination, action_potentials, time, voltage, limit_rad)
        print("true")
        print("0")

    except Exception as e:
        print("false")
        print(f"{type(e).__name__}: {str(e)}")

# ----------------------------------------------------------------------------------------------
